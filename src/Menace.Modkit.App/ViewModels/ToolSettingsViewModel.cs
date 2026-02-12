using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive;
using System.Text.Json;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// Settings for modders - extraction, assets, caching, validation.
/// </summary>
public sealed class ToolSettingsViewModel : ViewModelBase
{
    private string _assetsPathStatus = string.Empty;
    private string _cacheStatus = "Calculating...";
    private string _extractionStatus = string.Empty;
    private string _dependencyVersionsText = string.Empty;
    private string _validationStatus = string.Empty;
    private string _appUpdateStatus = string.Empty;
    private bool _hasAppUpdate = false;
    private string _latestAppVersion = string.Empty;
    private string _appDownloadUrl = string.Empty;
    private readonly SchemaService _schemaService;
    private readonly ExtractionValidator _validator;

    // GitHub repo for app update checks
    private const string AppGitHubOwner = "p0ss";
    private const string AppGitHubRepo = "MenaceAssetPacker";

    public ToolSettingsViewModel(IServiceProvider serviceProvider)
    {
        // Initialize schema and validator
        _schemaService = new SchemaService();
        _validator = new ExtractionValidator(_schemaService);

        // Load schema
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.json");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schema.json");
        _schemaService.LoadSchema(schemaPath);

        // Load from app settings
        ValidateAssetsPath();
        UpdateCacheStatus();
        LoadDependencyVersions();
        ValidateExtractedData();

        // Commands
        ViewCacheDetailsCommand = ReactiveCommand.Create(ViewCacheDetails);
        ClearCacheCommand = ReactiveCommand.Create(ClearCache);
        ForceExtractDataCommand = ReactiveCommand.CreateFromTask(ForceExtractDataAsync);
        ForceExtractAssetsCommand = ReactiveCommand.CreateFromTask(ForceExtractAssetsAsync);
        ValidateExtractionCommand = ReactiveCommand.Create(ValidateExtractedData);
        CheckForAppUpdateCommand = ReactiveCommand.CreateFromTask(CheckForAppUpdateAsync);
        OpenDownloadPageCommand = ReactiveCommand.Create(OpenDownloadPage);

        // Check for app updates on load
        _ = CheckForAppUpdateAsync();
    }

    public string ExtractedAssetsPath
    {
        get => AppSettings.Instance.ExtractedAssetsPath;
        set
        {
            if (AppSettings.Instance.ExtractedAssetsPath != value)
            {
                AppSettings.Instance.SetExtractedAssetsPath(value);
                this.RaisePropertyChanged();
                ValidateAssetsPath();
            }
        }
    }

    public string AssetsPathStatus
    {
        get => _assetsPathStatus;
        private set => this.RaiseAndSetIfChanged(ref _assetsPathStatus, value);
    }

    // Extraction Settings Properties
    public bool AutoUpdateOnGameChange
    {
        get => AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange;
        set
        {
            if (AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange != value)
            {
                AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange = value;
                AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
                this.RaisePropertyChanged();
            }
        }
    }

    public bool EnableCaching
    {
        get => AppSettings.Instance.ExtractionSettings.EnableCaching;
        set
        {
            if (AppSettings.Instance.ExtractionSettings.EnableCaching != value)
            {
                AppSettings.Instance.ExtractionSettings.EnableCaching = value;
                AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
                this.RaisePropertyChanged();
            }
        }
    }

    public bool KeepFullIL2CppDump
    {
        get => AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump;
        set
        {
            if (AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump != value)
            {
                AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump = value;
                AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
                this.RaisePropertyChanged();
            }
        }
    }

    public bool ShowExtractionProgress
    {
        get => AppSettings.Instance.ExtractionSettings.ShowExtractionProgress;
        set
        {
            if (AppSettings.Instance.ExtractionSettings.ShowExtractionProgress != value)
            {
                AppSettings.Instance.ExtractionSettings.ShowExtractionProgress = value;
                AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
                this.RaisePropertyChanged();
            }
        }
    }

    public AssetRipperProfileType SelectedAssetProfile
    {
        get => AppSettings.Instance.ExtractionSettings.AssetProfile;
        set
        {
            if (AppSettings.Instance.ExtractionSettings.AssetProfile != value)
            {
                AppSettings.Instance.ExtractionSettings.AssetProfile = value;
                AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
                this.RaisePropertyChanged();
            }
        }
    }

    public bool IsEssentialProfile
    {
        get => SelectedAssetProfile == AssetRipperProfileType.Essential;
        set { if (value) SelectedAssetProfile = AssetRipperProfileType.Essential; }
    }

    public bool IsStandardProfile
    {
        get => SelectedAssetProfile == AssetRipperProfileType.Standard;
        set { if (value) SelectedAssetProfile = AssetRipperProfileType.Standard; }
    }

    public bool IsCompleteProfile
    {
        get => SelectedAssetProfile == AssetRipperProfileType.Complete;
        set { if (value) SelectedAssetProfile = AssetRipperProfileType.Complete; }
    }

    public bool IsCustomProfile
    {
        get => SelectedAssetProfile == AssetRipperProfileType.Custom;
        set { if (value) SelectedAssetProfile = AssetRipperProfileType.Custom; }
    }

    public string CacheStatus
    {
        get => _cacheStatus;
        private set => this.RaiseAndSetIfChanged(ref _cacheStatus, value);
    }

    public string ExtractionStatus
    {
        get => _extractionStatus;
        private set => this.RaiseAndSetIfChanged(ref _extractionStatus, value);
    }

    public string DependencyVersionsText
    {
        get => _dependencyVersionsText;
        private set => this.RaiseAndSetIfChanged(ref _dependencyVersionsText, value);
    }

    public string ValidationStatus
    {
        get => _validationStatus;
        private set => this.RaiseAndSetIfChanged(ref _validationStatus, value);
    }

    // App Version & Update properties
    public string CurrentAppVersion => ModkitVersion.MelonVersion;

    public string AppUpdateStatus
    {
        get => _appUpdateStatus;
        private set => this.RaiseAndSetIfChanged(ref _appUpdateStatus, value);
    }

    public bool HasAppUpdate
    {
        get => _hasAppUpdate;
        private set => this.RaiseAndSetIfChanged(ref _hasAppUpdate, value);
    }

    public string LatestAppVersion
    {
        get => _latestAppVersion;
        private set => this.RaiseAndSetIfChanged(ref _latestAppVersion, value);
    }

    public string AppDownloadUrl
    {
        get => _appDownloadUrl;
        private set => this.RaiseAndSetIfChanged(ref _appDownloadUrl, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> ViewCacheDetailsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceExtractDataCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceExtractAssetsCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateExtractionCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckForAppUpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDownloadPageCommand { get; }

    private string GameInstallPath => AppSettings.Instance.GameInstallPath;

    private void ValidateAssetsPath()
    {
        var configured = ExtractedAssetsPath;

        if (string.IsNullOrWhiteSpace(configured))
        {
            // No custom path configured - show the auto-detected path
            var effective = AppSettings.GetEffectiveAssetsPath();
            if (effective != null)
            {
                try
                {
                    var count = Directory.GetFiles(effective, "*.*", SearchOption.AllDirectories).Length;
                    AssetsPathStatus = $"Auto-detected: {effective} ({count} files)";
                }
                catch
                {
                    AssetsPathStatus = $"Auto-detected: {effective}";
                }
            }
            else
            {
                AssetsPathStatus = "No extracted assets found. Run AssetRipper extraction first, or set a path.";
            }
            return;
        }

        if (!Directory.Exists(configured))
        {
            AssetsPathStatus = "Directory not found";
            return;
        }

        try
        {
            var count = Directory.GetFiles(configured, "*.*", SearchOption.AllDirectories).Length;
            AssetsPathStatus = $"Found {count} asset files";
        }
        catch (Exception ex)
        {
            AssetsPathStatus = $"Error reading directory: {ex.Message}";
        }
    }

    private void UpdateCacheStatus()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
        {
            CacheStatus = "No cache (set game install path in Loader Settings)";
            return;
        }

        var cachePath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache");
        if (!Directory.Exists(cachePath))
        {
            CacheStatus = "No cache";
            return;
        }

        try
        {
            var manifestPath = Path.Combine(cachePath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                CacheStatus = "No cache";
                return;
            }

            var manifestInfo = new FileInfo(manifestPath);
            var timeSince = DateTime.Now - manifestInfo.LastWriteTime;

            var cacheSize = GetDirectorySize(cachePath);
            var sizeStr = FormatBytes(cacheSize);

            string timeStr = timeSince.TotalHours < 1
                ? $"{(int)timeSince.TotalMinutes} minutes ago"
                : timeSince.TotalDays < 1
                    ? $"{(int)timeSince.TotalHours} hours ago"
                    : $"{(int)timeSince.TotalDays} days ago";

            CacheStatus = $"Last extraction: {timeStr} | Cache size: {sizeStr}";
        }
        catch
        {
            CacheStatus = "Error reading cache";
        }
    }

    private void LoadDependencyVersions()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "third_party", "versions.json");
            if (!File.Exists(path))
            {
                DependencyVersionsText = "versions.json not found";
                return;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var versions = JsonSerializer.Deserialize<DependencyVersions>(json, options);
            if (versions?.Dependencies == null || versions.Dependencies.Count == 0)
            {
                DependencyVersionsText = "No dependency info available";
                return;
            }

            DependencyVersionsText = string.Join(" | ",
                versions.Dependencies.Select(kv => $"{kv.Key} v{kv.Value.Version}"));
        }
        catch
        {
            DependencyVersionsText = "Error reading version info";
        }
    }

    private void ViewCacheDetails()
    {
        // TODO: Show detailed cache information dialog
    }

    private void ClearCache()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
            return;

        var cachePath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache");
        if (Directory.Exists(cachePath))
        {
            try
            {
                Directory.Delete(cachePath, true);
                UpdateCacheStatus();
            }
            catch (Exception ex)
            {
                CacheStatus = $"Error clearing cache: {ex.Message}";
            }
        }
    }

    private void ValidateExtractedData()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
        {
            ValidationStatus = "(set game install path in Loader Settings)";
            return;
        }

        var extractedDataPath = Path.Combine(GameInstallPath, "UserData", "ExtractedData");
        if (!Directory.Exists(extractedDataPath))
        {
            ValidationStatus = "";
            return;
        }

        try
        {
            var result = _validator.ValidateExtraction(extractedDataPath);
            if (result.IsValid)
            {
                ValidationStatus = $"✓ {result.GetSummary()}";
            }
            else
            {
                ValidationStatus = $"⚠ {result.GetSummary()}";
            }
        }
        catch (Exception ex)
        {
            ValidationStatus = $"Validation error: {ex.Message}";
            ModkitLog.Error($"[ToolSettingsViewModel] Validation failed: {ex}");
        }
    }

    private async Task ForceExtractDataAsync()
    {
        ClearCache();

        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
        {
            ExtractionStatus = "❌ Set game installation path in Loader Settings first";
            return;
        }

        var installer = new ModLoaderInstaller(GameInstallPath);

        // Ensure DataExtractor mod is deployed
        ExtractionStatus = "Deploying DataExtractor mod...";
        if (!installer.IsDataExtractorInstalled())
        {
            var installed = await installer.InstallDataExtractorAsync(s => ExtractionStatus = s);
            if (!installed)
                return;
        }
        else
        {
            // Update to latest version
            await installer.InstallDataExtractorAsync(s => ExtractionStatus = s);
        }

        // Delete old extracted data to force full re-extraction
        var extractedDataPath = Path.Combine(GameInstallPath, "UserData", "ExtractedData");
        if (Directory.Exists(extractedDataPath))
        {
            try
            {
                Directory.Delete(extractedDataPath, true);
                ExtractionStatus = "Cleared old extracted data";
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"Warning: could not clear old data: {ex.Message}";
            }
        }

        // Launch game to extract fresh data
        ExtractionStatus = "Launching game to re-extract template data...";
        var launched = await installer.LaunchGameAsync(s => ExtractionStatus = s);
        if (launched)
        {
            ExtractionStatus = "Game launched. Close it after reaching the main menu, then refresh the Data editor.";
            // Re-validate after user might have re-extracted
            ValidateExtractedData();
        }
    }

    private async Task ForceExtractAssetsAsync()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
        {
            ExtractionStatus = "❌ Set game installation path in Loader Settings first";
            return;
        }

        // Clear the asset rip manifest to force re-extraction
        var manifestPath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache", "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                // Read and clear just the asset rip fields
                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<ExtractionManifest>(json);
                if (manifest != null)
                {
                    manifest.AssetRipTimestamp = null;
                    manifest.GameAssemblyHash = string.Empty;
                    var updatedJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(manifestPath, updatedJson);
                }
            }
            catch
            {
                // If we can't update the manifest, just delete it
                try { File.Delete(manifestPath); } catch { }
            }
        }

        ExtractionStatus = "Starting AssetRipper extraction...";
        var assetRipper = new AssetRipperService();
        var success = await assetRipper.ExtractAssetsAsync(s => ExtractionStatus = s);

        if (success)
        {
            ValidateAssetsPath();
            UpdateCacheStatus();
        }
    }

    private async Task CheckForAppUpdateAsync()
    {
        AppUpdateStatus = "Checking for updates...";
        HasAppUpdate = false;

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MenaceModkit", ModkitVersion.Short));
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var apiUrl = $"https://api.github.com/repos/{AppGitHubOwner}/{AppGitHubRepo}/releases/latest";
            var response = await httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                AppUpdateStatus = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => "No releases found",
                    System.Net.HttpStatusCode.Forbidden => "Rate limited - try again later",
                    _ => $"Could not check for updates ({response.StatusCode})"
                };
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;

            // Normalize versions for comparison (remove 'v' prefix)
            var latestVersion = tagName.TrimStart('v', 'V');
            var currentVersion = ModkitVersion.MelonVersion;

            // Compare versions
            var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;

            LatestAppVersion = tagName;
            HasAppUpdate = hasUpdate;
            AppDownloadUrl = htmlUrl ?? $"https://github.com/{AppGitHubOwner}/{AppGitHubRepo}/releases/latest";

            if (hasUpdate)
            {
                AppUpdateStatus = $"Update available: {tagName}";
            }
            else
            {
                AppUpdateStatus = "You're up to date";
            }
        }
        catch (Exception ex)
        {
            AppUpdateStatus = $"Update check failed: {ex.Message}";
            ModkitLog.Error($"[ToolSettingsViewModel] App update check failed: {ex}");
        }
    }

    private void OpenDownloadPage()
    {
        if (string.IsNullOrEmpty(AppDownloadUrl))
            return;

        try
        {
            // Cross-platform way to open URL
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppDownloadUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ToolSettingsViewModel] Failed to open download page: {ex}");
        }
    }

    /// <summary>
    /// Compare two semver-ish version strings.
    /// Returns: positive if a > b, negative if a < b, 0 if equal.
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.', '-', '+');
        var partsB = b.Split('.', '-', '+');

        var maxParts = Math.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < maxParts; i++)
        {
            var partA = i < partsA.Length ? partsA[i] : "0";
            var partB = i < partsB.Length ? partsB[i] : "0";

            // Try numeric comparison first
            if (int.TryParse(partA, out var numA) && int.TryParse(partB, out var numB))
            {
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            else
            {
                // Fall back to string comparison
                var cmp = string.Compare(partA, partB, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;
            }
        }

        return 0;
    }

    private static long GetDirectorySize(string path)
    {
        var dirInfo = new DirectoryInfo(path);
        return dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
