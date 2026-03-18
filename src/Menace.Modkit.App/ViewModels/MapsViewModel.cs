using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// Tool mode for the map editor.
/// </summary>
public enum MapEditorTool
{
    Select,
    PaintZone,
    PlaceChunk,
    PaintTerrain,
    DrawPath,
    Eraser
}

/// <summary>
/// Zone types matching the game's MissionAreaType enum.
/// These define deployment/area zones on the map.
/// </summary>
public enum ZoneType
{
    /// <summary>Base deployment zone (default player spawn area).</summary>
    Base = 0,
    /// <summary>Chunk-based zone - positions relative to a chunk.</summary>
    Chunk = 1,
    /// <summary>South border of the map.</summary>
    SouthMapBorder = 2,
    /// <summary>East border of the map.</summary>
    EastMapBorder = 3,
    /// <summary>West border of the map.</summary>
    WestMapBorder = 4,
    /// <summary>North border of the map.</summary>
    NorthMapBorder = 5,
    /// <summary>Generic rectangle area.</summary>
    Rect = 6,
    /// <summary>Northeast corner of the map.</summary>
    NorthEastMapBorder = 7,
    /// <summary>Southeast corner of the map.</summary>
    SouthEastMapBorder = 8,
    /// <summary>Southwest corner of the map.</summary>
    SouthWestMapBorder = 9,
    /// <summary>Northwest corner of the map.</summary>
    NorthWestMapBorder = 10,
    /// <summary>Custom zone type for modding purposes.</summary>
    Custom = 100
}

/// <summary>
/// Terrain types for painting.
/// </summary>
public enum TerrainType
{
    Default,
    Trees,
    Water,
    HighGround,
    Road,
    Sand,
    Concrete
}

/// <summary>
/// Editor layer selection - determines which elements are shown/edited.
/// </summary>
public enum EditorLayer
{
    Zones,
    Paths,
    Chunks,
    Surfaces
}

/// <summary>
/// ViewModel for the Maps tab: visual map editor for custom maps.
/// </summary>
public sealed class MapsViewModel : ViewModelBase
{
    public const string CreateNewMapOption = "+ Create New Map...";
    private readonly ModpackManager _modpackManager;
    private readonly Random _brushRandom = new();

    public MapsViewModel()
    {
        _modpackManager = new ModpackManager();
        AvailableModpacks = new ObservableCollection<string>();
        AvailableMaps = new ObservableCollection<string>();
        Zones = new ObservableCollection<MapZoneViewModel>();
        Tiles = new ObservableCollection<TileOverrideViewModel>();
        Paths = new ObservableCollection<MapPathViewModel>();
        ChunkPlacements = new ObservableCollection<ChunkPlacementViewModel>();
        AvailableChunks = new ObservableCollection<ChunkTemplateViewModel>();
        PrefabCategories = new ObservableCollection<string>();
        TerrainTypes = new ObservableCollection<string>(
            Enum.GetNames(typeof(TerrainType)));
        ZoneTypes = new ObservableCollection<string>(
            Enum.GetNames(typeof(ZoneType)));

        LoadModpacks();
        LoadSampleChunks();
    }

    /// <summary>
    /// Load sample chunk templates for the browser.
    /// In production, these would come from extracted game data.
    /// </summary>
    private void LoadSampleChunks()
    {
        // Sample chunks based on common game structure types
        var sampleChunks = new[]
        {
            ("Bunker_Small", "Bunkers", 3, 3),
            ("Bunker_Medium", "Bunkers", 4, 4),
            ("Bunker_Large", "Bunkers", 5, 5),
            ("House_1", "Buildings", 4, 4),
            ("House_2", "Buildings", 5, 4),
            ("House_Corner", "Buildings", 4, 4),
            ("Warehouse", "Industrial", 6, 4),
            ("Factory", "Industrial", 8, 6),
            ("Tower_Watch", "Towers", 2, 2),
            ("Tower_Radio", "Towers", 3, 3),
            ("Cover_Sandbags", "Cover", 2, 1),
            ("Cover_Crates", "Cover", 2, 2),
            ("Cover_Barricade", "Cover", 3, 1),
            ("Wall_Section", "Walls", 4, 1),
            ("Wall_Corner", "Walls", 2, 2),
            ("Wall_Gate", "Walls", 3, 2),
            ("Outpost_Small", "Compounds", 8, 8),
            ("Outpost_Medium", "Compounds", 12, 10),
            ("Checkpoint", "Misc", 4, 3),
            ("Fuel_Depot", "Industrial", 5, 5),
        };

        foreach (var (name, category, width, height) in sampleChunks)
        {
            _allChunks.Add(new ChunkTemplateViewModel
            {
                Name = name,
                DisplayName = name.Replace('_', ' '),
                Category = category
            });
        }

        FilterChunks();

        // Start loading previews asynchronously
        _ = LoadChunkPreviewsAsync();
    }

    /// <summary>
    /// Cache of loaded preview bitmaps by chunk name to avoid re-rendering.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Bitmap?> _previewCache = new();

    /// <summary>
    /// Cached chunk layout data (loaded from exported JSON).
    /// </summary>
    private static Dictionary<string, ChunkLayoutData>? _chunkLayoutCache;

    /// <summary>
    /// Load preview images for all chunks asynchronously.
    /// Uses exported chunk layout JSON to render schematic top-down views.
    /// </summary>
    private async Task LoadChunkPreviewsAsync()
    {
        // First, try to load chunk layout data
        await LoadChunkLayoutDataAsync();

        // Generate schematic previews for all chunks
        var renderTasks = _allChunks.Select(RenderChunkSchematicAsync).ToList();
        await Task.WhenAll(renderTasks);

        ModkitLog.Info($"[MapsViewModel] Finished loading chunk previews");
    }

    /// <summary>
    /// Load chunk layout data from DataExtractor's ChunkTemplate.json output.
    /// This is generated automatically when the user extracts game data.
    /// </summary>
    private async Task LoadChunkLayoutDataAsync()
    {
        if (_chunkLayoutCache != null)
            return;

        _chunkLayoutCache = new Dictionary<string, ChunkLayoutData>(StringComparer.OrdinalIgnoreCase);

        // Look for ChunkTemplate.json in ExtractedData (DataExtractor output)
        var searchPaths = new List<string>();

        var gameInstallPath = AppSettings.Instance.GameInstallPath;
        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            // Primary location: DataExtractor output
            searchPaths.Add(Path.Combine(gameInstallPath, "UserData", "ExtractedData", "ChunkTemplate.json"));
        }

        var assetsPath = AppSettings.GetEffectiveAssetsPath();
        if (!string.IsNullOrEmpty(assetsPath))
        {
            // Alternative: alongside other extracted assets
            searchPaths.Add(Path.Combine(Path.GetDirectoryName(assetsPath) ?? "", "ExtractedData", "ChunkTemplate.json"));
        }

        foreach (var layoutPath in searchPaths)
        {
            if (!File.Exists(layoutPath))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(layoutPath);

                // Parse DataExtractor format (array of templates with m_ID, Width, Height, FixedPrefabs)
                var templates = JsonSerializer.Deserialize<List<ChunkTemplateExtracted>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (templates != null)
                {
                    foreach (var template in templates)
                    {
                        var name = template.m_ID ?? template.Name;
                        if (string.IsNullOrEmpty(name))
                            continue;

                        var layout = new ChunkLayoutData
                        {
                            Name = name,
                            Width = template.Width,
                            Height = template.Height,
                            Type = template.Type
                        };

                        // Convert FixedPrefabs to our format
                        if (template.FixedPrefabs != null)
                        {
                            layout.Prefabs = template.FixedPrefabs
                                .Select(p => new ChunkPrefabData
                                {
                                    X = p.X,
                                    Y = p.Y,
                                    Rotation = p.Rotation,
                                    Prefab = p.Prefab ?? p.PrefabName,
                                    Width = 1,
                                    Height = 1
                                })
                                .ToList();
                        }

                        _chunkLayoutCache[name] = layout;
                    }

                    ModkitLog.Info($"[MapsViewModel] Loaded {templates.Count} chunk layouts from {layoutPath}");
                    return;
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[MapsViewModel] Failed to load chunk layouts from {layoutPath}: {ex.Message}");
            }
        }

        ModkitLog.Info("[MapsViewModel] No ChunkTemplate.json found. Run data extraction from Settings to generate.");
    }

    /// <summary>
    /// Data structure matching DataExtractor's ChunkTemplate output.
    /// </summary>
    private class ChunkTemplateExtracted
    {
        [JsonPropertyName("m_ID")]
        public string? m_ID { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Width")]
        public int Width { get; set; }

        [JsonPropertyName("Height")]
        public int Height { get; set; }

        [JsonPropertyName("Type")]
        public int Type { get; set; }

        [JsonPropertyName("FixedPrefabs")]
        public List<FixedPrefabExtracted>? FixedPrefabs { get; set; }
    }

    /// <summary>
    /// Data structure matching DataExtractor's FixedPrefabEntry output.
    /// </summary>
    private class FixedPrefabExtracted
    {
        [JsonPropertyName("X")]
        public int X { get; set; }

        [JsonPropertyName("Y")]
        public int Y { get; set; }

        [JsonPropertyName("Rotation")]
        public int Rotation { get; set; }

        [JsonPropertyName("Prefab")]
        public string? Prefab { get; set; }

        [JsonPropertyName("PrefabName")]
        public string? PrefabName { get; set; }
    }

    /// <summary>
    /// Render a schematic top-down preview for a chunk.
    /// Shows prefab positions as colored rectangles within the chunk bounds.
    /// </summary>
    private async Task RenderChunkSchematicAsync(ChunkTemplateViewModel chunk)
    {
        chunk.IsLoadingPreview = true;

        try
        {
            // Check cache first
            if (_previewCache.TryGetValue(chunk.Name, out var cachedBitmap))
            {
                chunk.PreviewBitmap = cachedBitmap;
                return;
            }

            // Get layout data if available
            ChunkLayoutData? layoutData = null;
            _chunkLayoutCache?.TryGetValue(chunk.Name, out layoutData);

            // Render schematic on background thread
            var bitmap = await Task.Run(() => RenderChunkSchematic(chunk, layoutData));

            // Cache and set the result
            if (bitmap != null)
            {
                _previewCache.TryAdd(chunk.Name, bitmap);
                chunk.PreviewBitmap = bitmap;

                // Also store dimensions if we got them from layout data
                if (layoutData != null)
                {
                    chunk.LayoutWidth = layoutData.Width;
                    chunk.LayoutHeight = layoutData.Height;
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[MapsViewModel] Failed to render preview for {chunk.Name}: {ex.Message}");
        }
        finally
        {
            chunk.IsLoadingPreview = false;
        }
    }

    /// <summary>
    /// Render a top-down schematic of a chunk using SkiaSharp.
    /// </summary>
    private static Bitmap? RenderChunkSchematic(ChunkTemplateViewModel chunk, ChunkLayoutData? layout)
    {
        const int SIZE = 64;
        const int PADDING = 2;

        try
        {
            using var surface = SkiaSharp.SKSurface.Create(
                new SkiaSharp.SKImageInfo(SIZE, SIZE, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
            var canvas = surface.Canvas;

            // Dark background
            canvas.Clear(new SkiaSharp.SKColor(30, 30, 35));

            // Get chunk dimensions
            int chunkWidth = layout?.Width ?? 4;
            int chunkHeight = layout?.Height ?? 4;
            if (chunkWidth <= 0) chunkWidth = 4;
            if (chunkHeight <= 0) chunkHeight = 4;

            // Calculate scale to fit
            float availableSize = SIZE - PADDING * 2;
            float scale = Math.Min(availableSize / chunkWidth, availableSize / chunkHeight);
            float offsetX = (SIZE - chunkWidth * scale) / 2;
            float offsetY = (SIZE - chunkHeight * scale) / 2;

            // Draw chunk bounds
            using var boundsPaint = new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = new SkiaSharp.SKColor(80, 80, 90)
            };
            canvas.DrawRect(offsetX, offsetY, chunkWidth * scale, chunkHeight * scale, boundsPaint);

            // Draw prefabs if we have layout data
            if (layout?.Prefabs != null && layout.Prefabs.Count > 0)
            {
                using var prefabFill = new SkiaSharp.SKPaint
                {
                    IsAntialias = true,
                    Style = SkiaSharp.SKPaintStyle.Fill
                };
                using var prefabStroke = new SkiaSharp.SKPaint
                {
                    IsAntialias = true,
                    Style = SkiaSharp.SKPaintStyle.Stroke,
                    StrokeWidth = 0.5f,
                    Color = new SkiaSharp.SKColor(60, 60, 70)
                };

                foreach (var prefab in layout.Prefabs)
                {
                    // Color based on prefab name/type
                    var color = GetPrefabColor(prefab.Prefab);
                    prefabFill.Color = color;

                    float px = offsetX + prefab.X * scale;
                    float py = offsetY + prefab.Y * scale;
                    float pw = Math.Max(1, prefab.Width) * scale;
                    float ph = Math.Max(1, prefab.Height) * scale;

                    // Clamp to bounds
                    if (px < offsetX) { pw -= (offsetX - px); px = offsetX; }
                    if (py < offsetY) { ph -= (offsetY - py); py = offsetY; }
                    if (px + pw > offsetX + chunkWidth * scale) pw = offsetX + chunkWidth * scale - px;
                    if (py + ph > offsetY + chunkHeight * scale) ph = offsetY + chunkHeight * scale - py;

                    if (pw > 0 && ph > 0)
                    {
                        canvas.DrawRect(px, py, pw, ph, prefabFill);
                        canvas.DrawRect(px, py, pw, ph, prefabStroke);
                    }
                }
            }
            else
            {
                // No layout data - draw a simple building icon
                using var iconPaint = new SkiaSharp.SKPaint
                {
                    IsAntialias = true,
                    Style = SkiaSharp.SKPaintStyle.Fill,
                    Color = new SkiaSharp.SKColor(100, 140, 140, 180)
                };

                float margin = scale * 0.5f;
                canvas.DrawRect(
                    offsetX + margin,
                    offsetY + margin,
                    chunkWidth * scale - margin * 2,
                    chunkHeight * scale - margin * 2,
                    iconPaint);
            }

            // Convert to Avalonia bitmap
            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[MapsViewModel] RenderChunkSchematic failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get a color for a prefab based on its name.
    /// </summary>
    private static SkiaSharp.SKColor GetPrefabColor(string? prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return new SkiaSharp.SKColor(100, 100, 100, 200);

        var lower = prefabName.ToLowerInvariant();

        if (lower.Contains("wall") || lower.Contains("fence"))
            return new SkiaSharp.SKColor(139, 119, 101, 220); // Brown

        if (lower.Contains("cover") || lower.Contains("sandbag") || lower.Contains("crate"))
            return new SkiaSharp.SKColor(85, 107, 47, 200); // Olive

        if (lower.Contains("door") || lower.Contains("gate"))
            return new SkiaSharp.SKColor(160, 82, 45, 220); // Sienna

        if (lower.Contains("floor") || lower.Contains("ground"))
            return new SkiaSharp.SKColor(128, 128, 128, 150); // Gray

        if (lower.Contains("roof"))
            return new SkiaSharp.SKColor(105, 105, 105, 180); // DimGray

        if (lower.Contains("window"))
            return new SkiaSharp.SKColor(135, 206, 235, 200); // SkyBlue

        // Default building color
        return new SkiaSharp.SKColor(100, 149, 237, 200); // Cornflower blue
    }

    /// <summary>
    /// Reload chunk previews (useful after layout data is exported).
    /// </summary>
    public void ReloadChunkPreviews()
    {
        _previewCache.Clear();
        _chunkLayoutCache = null;
        _ = LoadChunkPreviewsAsync();
    }

    /// <summary>
    /// Data structure for chunk layout (matches exported JSON from game).
    /// </summary>
    public class ChunkLayoutData
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("prefabs")]
        public List<ChunkPrefabData>? Prefabs { get; set; }
    }

    /// <summary>
    /// Data structure for a prefab within a chunk layout.
    /// </summary>
    public class ChunkPrefabData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("rotation")]
        public int Rotation { get; set; }

        [JsonPropertyName("prefab")]
        public string? Prefab { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; } = 1;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 1;
    }

    // === Modpack and Map Selection ===

    public ObservableCollection<string> AvailableModpacks { get; }
    public ObservableCollection<string> AvailableMaps { get; }

    private string? _selectedModpack;
    public string? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            if (_selectedModpack != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedModpack, value);
                LoadMapsForModpack();
            }
        }
    }

    private string? _selectedMap;
    public string? SelectedMap
    {
        get => _selectedMap;
        set
        {
            if (_selectedMap != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedMap, value);
                LoadMap();
            }
        }
    }

    // === Current Map Configuration ===

    private string _mapId = "";
    public string MapId
    {
        get => _mapId;
        set => this.RaiseAndSetIfChanged(ref _mapId, value);
    }

    private string _mapName = "";
    public string MapName
    {
        get => _mapName;
        set => this.RaiseAndSetIfChanged(ref _mapName, value);
    }

    private int _mapSize = 42;
    public int MapSize
    {
        get => _mapSize;
        set
        {
            value = Math.Clamp(value, 20, 80);
            this.RaiseAndSetIfChanged(ref _mapSize, value);
        }
    }

    private int? _mapSeed;
    public int? MapSeed
    {
        get => _mapSeed;
        set => this.RaiseAndSetIfChanged(ref _mapSeed, value);
    }

    // === Collections ===

    public ObservableCollection<MapZoneViewModel> Zones { get; }
    public ObservableCollection<TileOverrideViewModel> Tiles { get; }
    public ObservableCollection<MapPathViewModel> Paths { get; }
    public ObservableCollection<ChunkPlacementViewModel> ChunkPlacements { get; }
    public ObservableCollection<ChunkTemplateViewModel> AvailableChunks { get; }
    public ObservableCollection<string> PrefabCategories { get; }
    public ObservableCollection<string> TerrainTypes { get; }
    public ObservableCollection<string> ZoneTypes { get; }

    // === Editor State ===

    private MapEditorTool _currentTool = MapEditorTool.Select;
    public MapEditorTool CurrentTool
    {
        get => _currentTool;
        set => this.RaiseAndSetIfChanged(ref _currentTool, value);
    }

    private EditorLayer _selectedLayer = EditorLayer.Zones;
    public EditorLayer SelectedLayer
    {
        get => _selectedLayer;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedLayer, value);
            // Auto-select appropriate tool for the layer
            CurrentTool = value switch
            {
                EditorLayer.Zones => MapEditorTool.PaintZone,
                EditorLayer.Paths => MapEditorTool.DrawPath,
                EditorLayer.Chunks => MapEditorTool.PlaceChunk,
                EditorLayer.Surfaces => MapEditorTool.PaintTerrain,
                _ => MapEditorTool.Select
            };
        }
    }

    private MapZoneViewModel? _selectedZone;
    public MapZoneViewModel? SelectedZone
    {
        get => _selectedZone;
        set => this.RaiseAndSetIfChanged(ref _selectedZone, value);
    }

    private MapPathViewModel? _selectedPath;
    public MapPathViewModel? SelectedPath
    {
        get => _selectedPath;
        set => this.RaiseAndSetIfChanged(ref _selectedPath, value);
    }

    private int _hoverTileX = -1;
    public int HoverTileX
    {
        get => _hoverTileX;
        set => this.RaiseAndSetIfChanged(ref _hoverTileX, value);
    }

    private int _hoverTileY = -1;
    public int HoverTileY
    {
        get => _hoverTileY;
        set => this.RaiseAndSetIfChanged(ref _hoverTileY, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    // Tool-specific settings

    private ChunkTemplateViewModel? _selectedChunk;
    public ChunkTemplateViewModel? SelectedChunk
    {
        get => _selectedChunk;
        set => this.RaiseAndSetIfChanged(ref _selectedChunk, value);
    }

    private int _chunkRotation = 0;
    public int ChunkRotation
    {
        get => _chunkRotation;
        set => this.RaiseAndSetIfChanged(ref _chunkRotation, value % 360);
    }

    private ChunkPlacementViewModel? _selectedChunkPlacement;
    public ChunkPlacementViewModel? SelectedChunkPlacement
    {
        get => _selectedChunkPlacement;
        set => this.RaiseAndSetIfChanged(ref _selectedChunkPlacement, value);
    }

    private TerrainType _selectedTerrainType = TerrainType.Default;
    public TerrainType SelectedTerrainType
    {
        get => _selectedTerrainType;
        set => this.RaiseAndSetIfChanged(ref _selectedTerrainType, value);
    }

    private ZoneType _selectedZoneType = ZoneType.Base;
    public ZoneType SelectedZoneType
    {
        get => _selectedZoneType;
        set => this.RaiseAndSetIfChanged(ref _selectedZoneType, value);
    }

    private int _pathWidth = 3;
    public int PathWidth
    {
        get => _pathWidth;
        set => this.RaiseAndSetIfChanged(ref _pathWidth, Math.Clamp(value, 1, 9));
    }

    // Brush settings for terrain painting
    // BrushSize is the radius: 0 = single tile, 1 = 3x3, 2 = 5x5, etc.
    private int _brushSize = 0;
    public int BrushSize
    {
        get => _brushSize;
        set => this.RaiseAndSetIfChanged(ref _brushSize, Math.Clamp(value, 0, 9));
    }

    private float _brushJitter = 0f;
    public float BrushJitter
    {
        get => _brushJitter;
        set => this.RaiseAndSetIfChanged(ref _brushJitter, Math.Clamp(value, 0f, 1f));
    }

    private bool _brushSmooth = true;
    public bool BrushSmooth
    {
        get => _brushSmooth;
        set => this.RaiseAndSetIfChanged(ref _brushSmooth, value);
    }

    private string _chunkSearchFilter = "";
    public string ChunkSearchFilter
    {
        get => _chunkSearchFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _chunkSearchFilter, value);
            FilterChunks();
        }
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set => this.RaiseAndSetIfChanged(ref _hasUnsavedChanges, value);
    }

    // === Load/Save Operations ===

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        AvailableModpacks.Add(CreateNewMapOption);
        var modpacks = _modpackManager.GetStagingModpacks();
        foreach (var mp in modpacks)
            AvailableModpacks.Add(mp.Name);

        if (AvailableModpacks.Count > 1 && _selectedModpack == null)
            SelectedModpack = AvailableModpacks[1];
    }

    private void LoadMapsForModpack()
    {
        AvailableMaps.Clear();
        AvailableMaps.Add(CreateNewMapOption);

        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewMapOption)
            return;

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
            return;

        var mapsDir = Path.Combine(modpack.Path, "custom_maps");
        if (!Directory.Exists(mapsDir))
            return;

        var files = Directory.GetFiles(mapsDir, "*.json");
        foreach (var file in files)
        {
            AvailableMaps.Add(Path.GetFileNameWithoutExtension(file));
        }
    }

    private void LoadMap()
    {
        ClearCurrentMap();

        if (string.IsNullOrEmpty(_selectedMap) || _selectedMap == CreateNewMapOption)
        {
            if (_selectedMap == CreateNewMapOption)
            {
                NewMap();
            }
            return;
        }

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
            return;

        var mapPath = Path.Combine(modpack.Path, "custom_maps", $"{_selectedMap}.json");
        if (!File.Exists(mapPath))
            return;

        try
        {
            var json = File.ReadAllText(mapPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var config = JsonSerializer.Deserialize<CustomMapConfigDto>(json, options);

            if (config != null)
            {
                MapId = config.Id ?? _selectedMap;
                MapName = config.Name ?? _selectedMap;
                MapSize = config.MapSize ?? 42;
                MapSeed = config.Seed;

                // Load zones
                if (config.Zones != null)
                {
                    foreach (var z in config.Zones)
                    {
                        Enum.TryParse<ZoneType>(z.ZoneType ?? "PlayerSpawn", out var zoneType);
                        var zoneVm = new MapZoneViewModel
                        {
                            Id = z.Id,
                            Name = z.Name,
                            ZoneType = zoneType,
                            X = z.X,
                            Y = z.Y,
                            Width = z.Width,
                            Height = z.Height,
                            Priority = z.Priority
                        };
                        Zones.Add(zoneVm);
                    }
                }

                // Load tiles (terrain painting)
                if (config.Tiles != null)
                {
                    foreach (var t in config.Tiles)
                    {
                        Tiles.Add(new TileOverrideViewModel
                        {
                            X = t.X,
                            Y = t.Y,
                            Terrain = t.Terrain,
                            Height = t.Height
                        });
                    }
                }

                // Load chunk placements
                if (config.Chunks != null)
                {
                    foreach (var c in config.Chunks)
                    {
                        ChunkPlacements.Add(new ChunkPlacementViewModel
                        {
                            X = c.X,
                            Y = c.Y,
                            ChunkTemplate = c.ChunkTemplate,
                            Rotation = c.Rotation
                        });
                    }
                }

                // Load paths
                if (config.Paths != null)
                {
                    foreach (var p in config.Paths)
                    {
                        var pathVm = new MapPathViewModel
                        {
                            Id = p.Id,
                            Type = p.Type,
                            Width = p.Width
                        };
                        if (p.Waypoints != null)
                        {
                            foreach (var wp in p.Waypoints)
                                pathVm.Waypoints.Add(new PathWaypointViewModel { X = wp.X, Y = wp.Y });
                        }
                        Paths.Add(pathVm);
                    }
                }

                StatusText = $"Loaded: {MapName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
            ModkitLog.Error($"[MapsViewModel] Load map failed: {ex.Message}");
        }

        HasUnsavedChanges = false;
    }

    public void NewMap()
    {
        ClearCurrentMap();
        MapId = $"new_map_{DateTime.Now:yyyyMMdd_HHmmss}";
        MapName = "New Map";
        MapSize = 42;
        MapSeed = null;
        HasUnsavedChanges = true;
        StatusText = "New map created";
    }

    private void ClearCurrentMap()
    {
        Zones.Clear();
        Tiles.Clear();
        Paths.Clear();
        ChunkPlacements.Clear();
        SelectedZone = null;
        SelectedPath = null;
        SelectedChunkPlacement = null;
        MapId = "";
        MapName = "";
        MapSize = 42;
        MapSeed = null;
    }

    public void SaveMap()
    {
        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewMapOption)
        {
            StatusText = "Select a modpack first";
            return;
        }

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
        {
            StatusText = "Modpack not found";
            return;
        }

        var mapsDir = Path.Combine(modpack.Path, "custom_maps");
        Directory.CreateDirectory(mapsDir);

        var config = BuildConfigDto();
        var fileName = string.IsNullOrEmpty(MapId) ? "untitled" : MapId;
        var mapPath = Path.Combine(mapsDir, $"{fileName}.json");

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(mapPath, json);

            HasUnsavedChanges = false;
            StatusText = $"Saved: {mapPath}";

            // Refresh map list if this is a new map
            if (!AvailableMaps.Contains(fileName))
            {
                AvailableMaps.Add(fileName);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
            ModkitLog.Error($"[MapsViewModel] Save map failed: {ex.Message}");
        }
    }

    private CustomMapConfigDto BuildConfigDto()
    {
        var config = new CustomMapConfigDto
        {
            Id = MapId,
            Name = MapName,
            MapSize = MapSize,
            Seed = MapSeed,
            Zones = Zones.Select(z => new MapZoneDto
            {
                Id = z.Id,
                Name = z.Name,
                ZoneType = z.ZoneType.ToString(),
                X = z.X,
                Y = z.Y,
                Width = z.Width,
                Height = z.Height,
                Priority = z.Priority
            }).ToList(),
            Tiles = Tiles.Where(t => t.HasAnyOverride).Select(t => new TileOverrideDto
            {
                X = t.X,
                Y = t.Y,
                Terrain = t.Terrain ?? "",
                Height = t.Height
            }).ToList(),
            Paths = Paths.Select(p => new MapPathDto
            {
                Id = p.Id,
                Type = p.Type,
                Width = p.Width,
                Waypoints = p.Waypoints.Select(wp => new PathWaypointDto { X = wp.X, Y = wp.Y }).ToList()
            }).ToList(),
            Chunks = ChunkPlacements.Select(c => new ChunkPlacementDto
            {
                X = c.X,
                Y = c.Y,
                ChunkTemplate = c.ChunkTemplate,
                Rotation = c.Rotation
            }).ToList()
        };
        return config;
    }

    // === Tile Operations ===

    public void OnTileClicked(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapSize || y >= MapSize)
            return;

        HasUnsavedChanges = true;

        switch (CurrentTool)
        {
            case MapEditorTool.Select:
                // Select chunk placement, zone, or tile at position
                var chunk = ChunkPlacements.FirstOrDefault(c => c.X == x && c.Y == y);
                if (chunk != null)
                {
                    SelectedChunkPlacement = chunk;
                    SelectedZone = null;
                }
                else
                {
                    var zone = Zones.FirstOrDefault(z => z.Contains(x, y));
                    if (zone != null)
                    {
                        SelectedZone = zone;
                        SelectedChunkPlacement = null;
                    }
                }
                break;

            case MapEditorTool.PlaceChunk:
                PlaceChunkAtTile(x, y);
                break;

            case MapEditorTool.PaintTerrain:
                PaintTerrainAtTile(x, y);
                break;

            case MapEditorTool.DrawPath:
                AddPathWaypoint(x, y);
                break;

            case MapEditorTool.Eraser:
                EraseTile(x, y);
                break;
        }

        StatusText = $"Tile ({x}, {y}) | Tool: {CurrentTool}";
    }

    public void OnTileDrag(int startX, int startY, int endX, int endY)
    {
        if (CurrentTool == MapEditorTool.PaintZone)
        {
            // Create or resize gameplay zone
            var minX = Math.Min(startX, endX);
            var minY = Math.Min(startY, endY);
            var width = Math.Abs(endX - startX) + 1;
            var height = Math.Abs(endY - startY) + 1;

            if (SelectedZone != null)
            {
                SelectedZone.X = minX;
                SelectedZone.Y = minY;
                SelectedZone.Width = width;
                SelectedZone.Height = height;
            }
            else
            {
                var newZone = new MapZoneViewModel
                {
                    Id = $"zone_{Zones.Count + 1}",
                    Name = $"{SelectedZoneType} Zone",
                    ZoneType = SelectedZoneType,
                    X = minX,
                    Y = minY,
                    Width = width,
                    Height = height,
                    Priority = 0
                };
                Zones.Add(newZone);
                SelectedZone = newZone;
            }

            HasUnsavedChanges = true;
            StatusText = $"Zone: ({minX},{minY}) to ({minX + width},{minY + height})";
        }
        else if (CurrentTool == MapEditorTool.PaintTerrain)
        {
            // Paint terrain in a line/area
            var minX = Math.Min(startX, endX);
            var maxX = Math.Max(startX, endX);
            var minY = Math.Min(startY, endY);
            var maxY = Math.Max(startY, endY);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (x >= 0 && y >= 0 && x < MapSize && y < MapSize)
                    {
                        PaintTerrainAtTile(x, y);
                    }
                }
            }

            HasUnsavedChanges = true;
            StatusText = $"Painted {SelectedTerrainType} from ({minX},{minY}) to ({maxX},{maxY})";
        }
    }

    private void PlaceChunkAtTile(int x, int y)
    {
        if (SelectedChunk == null)
        {
            StatusText = "Select a chunk from the browser first";
            return;
        }

        // Check for existing chunk at this position
        var existing = ChunkPlacements.FirstOrDefault(c => c.X == x && c.Y == y);
        if (existing != null)
        {
            // Update existing placement
            existing.ChunkTemplate = SelectedChunk.Name;
            existing.Rotation = ChunkRotation;
        }
        else
        {
            // Create new placement
            var placement = new ChunkPlacementViewModel
            {
                X = x,
                Y = y,
                ChunkTemplate = SelectedChunk.Name,
                Rotation = ChunkRotation
            };
            ChunkPlacements.Add(placement);
            SelectedChunkPlacement = placement;
        }

        StatusText = $"Placed chunk '{SelectedChunk.Name}' at ({x}, {y})";
    }

    private void PaintTerrainAtTile(int x, int y)
    {
        var terrainName = SelectedTerrainType.ToString();
        var radius = BrushSize; // BrushSize IS the radius
        var tilesModified = 0;

        // Paint in pattern based on radius
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var tileX = x + dx;
                var tileY = y + dy;

                // Skip out-of-bounds tiles
                if (tileX < 0 || tileY < 0 || tileX >= MapSize || tileY >= MapSize)
                    continue;

                // For circular brush, check distance from center
                if (BrushSmooth)
                {
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > radius + 0.5)
                        continue;
                }

                // Jitter controls fill density (0 = solid, 1 = very sparse)
                // Always paint center tile
                if (BrushJitter > 0 && (dx != 0 || dy != 0))
                {
                    if (_brushRandom.NextDouble() < BrushJitter)
                        continue;
                }

                PaintSingleTile(tileX, tileY, terrainName);
                tilesModified++;
            }
        }

        StatusText = radius > 0
            ? $"Painted {terrainName} ({tilesModified} tiles) at ({x}, {y})"
            : $"Painted {terrainName} at ({x}, {y})";
    }

    private void PaintSingleTile(int x, int y, string terrainName)
    {
        var existing = Tiles.FirstOrDefault(t => t.X == x && t.Y == y);

        if (existing != null)
        {
            existing.Terrain = terrainName;
            if (!existing.HasAnyOverride)
                Tiles.Remove(existing);
        }
        else if (SelectedTerrainType != TerrainType.Default)
        {
            Tiles.Add(new TileOverrideViewModel { X = x, Y = y, Terrain = terrainName });
        }
    }

    private void AddPathWaypoint(int x, int y)
    {
        if (SelectedPath == null)
        {
            // Create new path
            var newPath = new MapPathViewModel
            {
                Id = $"path_{Paths.Count + 1}",
                Type = "Road",
                Width = PathWidth
            };
            newPath.Waypoints.Add(new PathWaypointViewModel { X = x, Y = y });
            Paths.Add(newPath);
            SelectedPath = newPath;
        }
        else
        {
            SelectedPath.Waypoints.Add(new PathWaypointViewModel { X = x, Y = y });
        }
    }

    private void EraseTile(int x, int y)
    {
        // Remove chunk placement at position
        var chunk = ChunkPlacements.FirstOrDefault(c => c.X == x && c.Y == y);
        if (chunk != null)
        {
            ChunkPlacements.Remove(chunk);
            if (SelectedChunkPlacement == chunk)
                SelectedChunkPlacement = null;
            StatusText = $"Removed chunk at ({x}, {y})";
            return;
        }

        // Remove tile override at position
        var tile = Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
        if (tile != null)
        {
            Tiles.Remove(tile);
            StatusText = $"Removed tile override at ({x}, {y})";
        }
    }

    // === Zone Operations ===

    public void AddZone()
    {
        var zone = new MapZoneViewModel
        {
            Id = $"zone_{Zones.Count + 1}",
            Name = $"{SelectedZoneType} Zone",
            ZoneType = SelectedZoneType,
            X = MapSize / 4,
            Y = MapSize / 4,
            Width = MapSize / 2,
            Height = MapSize / 2,
            Priority = Zones.Count
        };
        Zones.Add(zone);
        SelectedZone = zone;
        HasUnsavedChanges = true;
        StatusText = $"Added {SelectedZoneType} zone";
    }

    public void RemoveSelectedZone()
    {
        if (SelectedZone != null)
        {
            Zones.Remove(SelectedZone);
            SelectedZone = null;
            HasUnsavedChanges = true;
        }
    }

    // === Chunk Operations ===

    public void RemoveSelectedChunkPlacement()
    {
        if (SelectedChunkPlacement != null)
        {
            ChunkPlacements.Remove(SelectedChunkPlacement);
            SelectedChunkPlacement = null;
            HasUnsavedChanges = true;
            StatusText = "Removed chunk placement";
        }
    }

    public void RotateSelectedChunk(int degrees)
    {
        if (SelectedChunkPlacement != null)
        {
            SelectedChunkPlacement.Rotation = (SelectedChunkPlacement.Rotation + degrees) % 360;
            if (SelectedChunkPlacement.Rotation < 0)
                SelectedChunkPlacement.Rotation += 360;
            HasUnsavedChanges = true;
            StatusText = $"Rotated chunk to {SelectedChunkPlacement.Rotation}°";
        }
        else
        {
            ChunkRotation = (ChunkRotation + degrees) % 360;
            if (ChunkRotation < 0)
                ChunkRotation += 360;
            StatusText = $"Placement rotation set to {ChunkRotation}°";
        }
    }

    /// <summary>
    /// Load available chunks from extracted template data.
    /// </summary>
    public void LoadAvailableChunks(string extractedDataPath)
    {
        AvailableChunks.Clear();
        _allChunks.Clear();

        // Look for ChunkDefinition or similar templates
        var chunkFile = Path.Combine(extractedDataPath, "ChunkDefinition.json");
        if (!File.Exists(chunkFile))
        {
            // Try EntityTemplate with chunk-like names
            chunkFile = Path.Combine(extractedDataPath, "EntityTemplate.json");
        }

        if (!File.Exists(chunkFile))
        {
            StatusText = "No chunk templates found in extracted data";
            return;
        }

        try
        {
            var json = File.ReadAllText(chunkFile);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var nameProp))
                    {
                        var name = nameProp.GetString() ?? "";
                        // Filter to chunk-like templates (buildings, structures)
                        if (IsChunkLikeTemplate(name))
                        {
                            var chunk = new ChunkTemplateViewModel
                            {
                                Name = name,
                                Category = GetChunkCategory(name),
                                DisplayName = GetDisplayName(name)
                            };
                            _allChunks.Add(chunk);
                        }
                    }
                }
            }

            FilterChunks();
            StatusText = $"Loaded {_allChunks.Count} chunk templates";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to load chunks: {ex.Message}";
            ModkitLog.Error($"[MapsViewModel] LoadAvailableChunks: {ex.Message}");
        }
    }

    private readonly List<ChunkTemplateViewModel> _allChunks = new();

    private void FilterChunks()
    {
        AvailableChunks.Clear();
        var filter = ChunkSearchFilter?.ToLowerInvariant() ?? "";

        foreach (var chunk in _allChunks)
        {
            if (string.IsNullOrEmpty(filter) ||
                chunk.Name.ToLowerInvariant().Contains(filter) ||
                chunk.DisplayName.ToLowerInvariant().Contains(filter) ||
                chunk.Category.ToLowerInvariant().Contains(filter))
            {
                AvailableChunks.Add(chunk);
            }
        }
    }

    private bool IsChunkLikeTemplate(string name)
    {
        // Filter to building/structure templates
        var lower = name.ToLowerInvariant();
        return lower.Contains("building") ||
               lower.Contains("structure") ||
               lower.Contains("bunker") ||
               lower.Contains("house") ||
               lower.Contains("warehouse") ||
               lower.Contains("tower") ||
               lower.Contains("outpost") ||
               lower.Contains("chunk") ||
               lower.Contains("cover") ||
               lower.Contains("wall");
    }

    private string GetChunkCategory(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("building")) return "Buildings";
        if (lower.Contains("bunker")) return "Bunkers";
        if (lower.Contains("house")) return "Houses";
        if (lower.Contains("warehouse")) return "Industrial";
        if (lower.Contains("tower")) return "Towers";
        if (lower.Contains("wall")) return "Walls";
        if (lower.Contains("cover")) return "Cover";
        return "Misc";
    }

    private string GetDisplayName(string name)
    {
        // Convert template name to display name
        var parts = name.Split('.');
        return parts.Length > 0 ? parts[^1].Replace('_', ' ') : name;
    }

    // === Path Operations ===

    public void AddPath()
    {
        var path = new MapPathViewModel
        {
            Id = $"path_{Paths.Count + 1}",
            Type = "Road",
            Width = PathWidth
        };
        Paths.Add(path);
        SelectedPath = path;
        HasUnsavedChanges = true;
        CurrentTool = MapEditorTool.DrawPath;
    }

    public void RemoveSelectedPath()
    {
        if (SelectedPath != null)
        {
            Paths.Remove(SelectedPath);
            SelectedPath = null;
            HasUnsavedChanges = true;
        }
    }

    public void FinishPath()
    {
        SelectedPath = null;
        CurrentTool = MapEditorTool.Select;
    }

    public void RefreshAll()
    {
        LoadModpacks();
    }

    // === Tool Selection ===

    public void SelectTool(MapEditorTool tool)
    {
        CurrentTool = tool;
        StatusText = $"Tool: {tool}";

        // Finish any in-progress path when switching away from DrawPath
        if (tool != MapEditorTool.DrawPath && SelectedPath != null)
        {
            SelectedPath = null;
        }
    }

    // === Utility ===

    public TileOverrideViewModel? GetTileAt(int x, int y)
    {
        return Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
    }

    public MapZoneViewModel? GetZoneAt(int x, int y)
    {
        return Zones.OrderByDescending(z => z.Priority).FirstOrDefault(z => z.Contains(x, y));
    }
}

// === View Models for map elements ===

public class MapZoneViewModel : ViewModelBase
{
    private string _id = "";
    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    private string _name = "";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private ZoneType _zoneType = ZoneType.Base;
    public ZoneType ZoneType
    {
        get => _zoneType;
        set => this.RaiseAndSetIfChanged(ref _zoneType, value);
    }

    private int _x;
    public int X
    {
        get => _x;
        set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    private int _y;
    public int Y
    {
        get => _y;
        set => this.RaiseAndSetIfChanged(ref _y, value);
    }

    private int _width = 10;
    public int Width
    {
        get => _width;
        set => this.RaiseAndSetIfChanged(ref _width, Math.Max(1, value));
    }

    private int _height = 10;
    public int Height
    {
        get => _height;
        set => this.RaiseAndSetIfChanged(ref _height, Math.Max(1, value));
    }

    private int _priority;
    public int Priority
    {
        get => _priority;
        set => this.RaiseAndSetIfChanged(ref _priority, value);
    }

    /// <summary>
    /// Display color for the zone based on its type (matches game's MissionAreaType).
    /// </summary>
    public string Color => ZoneType switch
    {
        ZoneType.Base => "#4488FF",            // Blue - player deployment
        ZoneType.Chunk => "#8844FF",           // Purple - chunk-based
        ZoneType.Rect => "#FF8844",            // Orange - generic rectangle
        ZoneType.NorthMapBorder => "#44FF88",  // Teal
        ZoneType.SouthMapBorder => "#88FF44",  // Lime
        ZoneType.EastMapBorder => "#FF44FF",   // Magenta
        ZoneType.WestMapBorder => "#44FFFF",   // Cyan
        ZoneType.NorthEastMapBorder => "#FFFF44", // Yellow
        ZoneType.SouthEastMapBorder => "#FF4444", // Red
        ZoneType.SouthWestMapBorder => "#44FF44", // Green
        ZoneType.NorthWestMapBorder => "#4444FF", // Dark Blue
        ZoneType.Custom => "#888888",          // Gray
        _ => "#FFFFFF"                         // White
    };

    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// ViewModel for a chunk template available for placement.
/// </summary>
public class ChunkTemplateViewModel : ViewModelBase
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";

    /// <summary>
    /// Path to preview image if available.
    /// </summary>
    public string? PreviewImagePath { get; set; }

    /// <summary>
    /// Path to the GLB mesh file for this chunk.
    /// </summary>
    public string? MeshPath { get; set; }

    /// <summary>
    /// Chunk width in tiles (from layout data).
    /// </summary>
    public int LayoutWidth { get; set; }

    /// <summary>
    /// Chunk height in tiles (from layout data).
    /// </summary>
    public int LayoutHeight { get; set; }

    private Bitmap? _previewBitmap;
    /// <summary>
    /// Preview bitmap rendered as a top-down schematic.
    /// Shows prefab positions within the chunk.
    /// </summary>
    public Bitmap? PreviewBitmap
    {
        get => _previewBitmap;
        set => this.RaiseAndSetIfChanged(ref _previewBitmap, value);
    }

    private bool _isLoadingPreview;
    /// <summary>
    /// True while the preview is being loaded.
    /// </summary>
    public bool IsLoadingPreview
    {
        get => _isLoadingPreview;
        set => this.RaiseAndSetIfChanged(ref _isLoadingPreview, value);
    }

    /// <summary>
    /// Display size info for tooltip.
    /// </summary>
    public string SizeInfo => LayoutWidth > 0 && LayoutHeight > 0
        ? $"{LayoutWidth}x{LayoutHeight}"
        : "";

    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel for a placed chunk on the map.
/// </summary>
public class ChunkPlacementViewModel : ViewModelBase
{
    private int _x;
    public int X
    {
        get => _x;
        set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    private int _y;
    public int Y
    {
        get => _y;
        set => this.RaiseAndSetIfChanged(ref _y, value);
    }

    private string _chunkTemplate = "";
    public string ChunkTemplate
    {
        get => _chunkTemplate;
        set => this.RaiseAndSetIfChanged(ref _chunkTemplate, value);
    }

    private int _rotation;
    public int Rotation
    {
        get => _rotation;
        set => this.RaiseAndSetIfChanged(ref _rotation, value % 360);
    }

    public string DisplayText => $"{ChunkTemplate} @ ({X},{Y}) R{Rotation}°";
}

public class TileOverrideViewModel : ViewModelBase
{
    public int X { get; set; }
    public int Y { get; set; }

    private string? _terrain;
    public string? Terrain
    {
        get => _terrain;
        set => this.RaiseAndSetIfChanged(ref _terrain, value);
    }

    private float? _height;
    public float? Height
    {
        get => _height;
        set => this.RaiseAndSetIfChanged(ref _height, value);
    }

    public bool HasAnyOverride =>
        Height.HasValue || !string.IsNullOrEmpty(Terrain);

    /// <summary>
    /// Display color for the tile based on terrain type.
    /// </summary>
    public string Color => Terrain switch
    {
        "Trees" => "#228B22",      // Forest green
        "Water" => "#4169E1",      // Royal blue
        "HighGround" => "#8B4513", // Saddle brown
        "Road" => "#696969",       // Dim gray
        "Sand" => "#F4A460",       // Sandy brown
        "Concrete" => "#808080",   // Gray
        _ => "#FFFFFF"             // White (default)
    };
}

public class MapPathViewModel : ViewModelBase
{
    private string _id = "";
    public string Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    private string _type = "Road";
    public string Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    private int _width = 3;
    public int Width
    {
        get => _width;
        set => this.RaiseAndSetIfChanged(ref _width, Math.Clamp(value, 1, 9));
    }

    public ObservableCollection<PathWaypointViewModel> Waypoints { get; } = new();
}

public class PathWaypointViewModel : ViewModelBase
{
    public int X { get; set; }
    public int Y { get; set; }
}

// === DTOs for JSON serialization ===

public class CustomMapConfigDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    [JsonPropertyName("mapSize")]
    public int? MapSize { get; set; }

    [JsonPropertyName("zones")]
    public List<MapZoneDto> Zones { get; set; } = new();

    [JsonPropertyName("tiles")]
    public List<TileOverrideDto> Tiles { get; set; } = new();

    [JsonPropertyName("paths")]
    public List<MapPathDto> Paths { get; set; } = new();

    [JsonPropertyName("chunks")]
    public List<ChunkPlacementDto> Chunks { get; set; } = new();
}

public class MapZoneDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string ZoneType { get; set; } = "PlayerSpawn";

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

public class ChunkPlacementDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("template")]
    public string ChunkTemplate { get; set; } = "";

    [JsonPropertyName("rotation")]
    public int Rotation { get; set; }
}

public class TileOverrideDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("terrain")]
    public string Terrain { get; set; } = "";

    [JsonPropertyName("height")]
    public float? Height { get; set; }
}

public class MapPathDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Road";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("waypoints")]
    public List<PathWaypointDto> Waypoints { get; set; } = new();
}

public class PathWaypointDto
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}
