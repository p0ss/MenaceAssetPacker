using ReactiveUI;
using System;
using System.Collections.Generic;
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
/// Event args for requesting the extraction dialog.
/// </summary>
public class ExtractionDialogRequestEventArgs : EventArgs
{
    public List<string> DeployedModpackNames { get; }
    public DeployManager DeployManager { get; }
    public ModpackManager ModpackManager { get; }
    public TaskCompletionSource<bool> Result { get; } = new();

    public ExtractionDialogRequestEventArgs(
        List<string> deployedModpackNames,
        DeployManager deployManager,
        ModpackManager modpackManager)
    {
        DeployedModpackNames = deployedModpackNames;
        DeployManager = deployManager;
        ModpackManager = modpackManager;
    }
}

/// <summary>
/// Event args for requesting a recovery dialog after app restart.
/// </summary>
public class RecoveryDialogRequestEventArgs : EventArgs
{
    public PendingRedeployState PendingState { get; }
    public DeployManager DeployManager { get; }
    public TaskCompletionSource<bool> Result { get; } = new();

    public RecoveryDialogRequestEventArgs(PendingRedeployState pendingState, DeployManager deployManager)
    {
        PendingState = pendingState;
        DeployManager = deployManager;
    }
}

/// <summary>
/// <summary>
/// Event args for requesting the update/setup flow.
/// </summary>
public class UpdateFlowRequestEventArgs : EventArgs
{
    public TaskCompletionSource<bool> Result { get; } = new();
}

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
    private string _otherChannelVersion = string.Empty;
    private string _channelStatusMessage = string.Empty;
    private readonly SchemaService _schemaService;
    private readonly ExtractionValidator _validator;
    private readonly ModpackManager _modpackManager;
    private readonly DeployManager _deployManager;

    // GitHub repo for app update checks
    private const string AppGitHubOwner = "p0ss";
    private const string AppGitHubRepo = "MenaceAssetPacker";

    /// <summary>
    /// Event raised when the extraction dialog should be shown.
    /// The View subscribes to this and shows the ExtractionDialog.
    /// </summary>
    public event EventHandler<ExtractionDialogRequestEventArgs>? ExtractionDialogRequested;

    /// <summary>
    /// Event raised when a recovery dialog should be shown after app restart.
    /// </summary>
    public event EventHandler<RecoveryDialogRequestEventArgs>? RecoveryDialogRequested;

    /// <summary>
    /// Event raised when the user requests to run the update/setup flow.
    /// </summary>
    public event EventHandler<UpdateFlowRequestEventArgs>? UpdateFlowRequested;

    /// <summary>
    /// Event raised when the modpack list needs to be refreshed (e.g., after update/undeploy).
    /// </summary>
    public event EventHandler? ModpacksNeedRefresh;

    public ToolSettingsViewModel(IServiceProvider serviceProvider)
    {
        // Initialize schema and validator
        _schemaService = new SchemaService();
        _validator = new ExtractionValidator(_schemaService);

        // Initialize modpack and deploy managers
        _modpackManager = new ModpackManager();
        _deployManager = new DeployManager(_modpackManager);

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
        StartUpdateCommand = ReactiveCommand.CreateFromTask(StartUpdateFlowAsync);

        // Check for app updates on load and refresh channel info
        _ = RefreshChannelInfoAsync();

        // Check for pending redeploy from previous session
        _ = CheckPendingRedeployAsync();
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

    /// <summary>
    /// When enabled, game data extraction runs automatically on game launch.
    /// Disable to prevent the freeze for users who only play mods (don't create them).
    /// </summary>
    public bool EnableAutoExtraction
    {
        get => AppSettings.Instance.HasUsedModdingTools;
        set
        {
            if (AppSettings.Instance.HasUsedModdingTools != value)
            {
                AppSettings.Instance.SetHasUsedModdingTools(value);
                this.RaisePropertyChanged();
            }
        }
    }

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

    // Release Channel properties

    /// <summary>
    /// True if the user is on the stable channel.
    /// </summary>
    public bool IsStableChannel
    {
        get => AppSettings.Instance.UpdateChannel == "stable";
        set
        {
            if (value && !IsStableChannel)
            {
                AppSettings.Instance.SetUpdateChannel("stable");
                ComponentManager.Instance.InvalidateManifestCache();
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsBetaChannel));
                this.RaisePropertyChanged(nameof(ChannelStatusMessage));
                _ = RefreshChannelInfoAsync();
            }
        }
    }

    /// <summary>
    /// True if the user is on the beta channel.
    /// </summary>
    public bool IsBetaChannel
    {
        get => AppSettings.Instance.UpdateChannel == "beta";
        set
        {
            if (value && !IsBetaChannel)
            {
                AppSettings.Instance.SetUpdateChannel("beta");
                ComponentManager.Instance.InvalidateManifestCache();
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(IsStableChannel));
                this.RaisePropertyChanged(nameof(ChannelStatusMessage));
                _ = RefreshChannelInfoAsync();
            }
        }
    }

    /// <summary>
    /// Version available on the other channel (if different from current).
    /// </summary>
    public string OtherChannelVersion
    {
        get => _otherChannelVersion;
        private set => this.RaiseAndSetIfChanged(ref _otherChannelVersion, value);
    }

    /// <summary>
    /// Status message about the current channel (e.g., "v31.0.2 (Beta) - v32.0.0 available on Stable").
    /// </summary>
    public string ChannelStatusMessage
    {
        get => _channelStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _channelStatusMessage, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> ViewCacheDetailsCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceExtractDataCommand { get; }
    public ReactiveCommand<Unit, Unit> ForceExtractAssetsCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateExtractionCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckForAppUpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> StartUpdateCommand { get; }

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
            // Use channel-specific manifest
            var filename = AppSettings.Instance.IsBetaChannel ? "versions-beta.json" : "versions.json";
            var path = Path.Combine(AppContext.BaseDirectory, "third_party", filename);
            if (!File.Exists(path))
            {
                // Fall back to stable manifest
                path = Path.Combine(AppContext.BaseDirectory, "third_party", "versions.json");
            }
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

    /// <summary>
    /// Check if extraction is pending (flag file exists).
    /// </summary>
    public bool IsExtractionPending
    {
        get
        {
            if (string.IsNullOrEmpty(GameInstallPath))
                return false;
            var installer = new ModLoaderInstaller(GameInstallPath);
            return installer.IsForceExtractionPending();
        }
    }

    private async Task ForceExtractDataAsync()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
        {
            ExtractionStatus = "❌ Set game installation path in Loader Settings first";
            return;
        }

        // Check if mods are deployed - if so, we need to undeploy them first
        var deployState = _deployManager.GetDeployState();
        if (deployState.DeployedModpacks.Count > 0)
        {
            var deployedNames = deployState.DeployedModpacks
                .Select(m => m.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            if (deployedNames.Count > 0)
            {
                ExtractionStatus = "Mods detected - showing extraction dialog...";

                // Request the dialog from the View
                var args = new ExtractionDialogRequestEventArgs(
                    deployedNames,
                    _deployManager,
                    _modpackManager);

                ExtractionDialogRequested?.Invoke(this, args);

                // Wait for the dialog to complete
                try
                {
                    var success = await args.Result.Task;
                    if (success)
                    {
                        ExtractionStatus = "✓ Extraction complete, mods redeployed";
                        ValidateExtractedData();
                        UpdateCacheStatus();
                    }
                    else
                    {
                        ExtractionStatus = "Extraction cancelled or failed";
                    }
                }
                catch (Exception ex)
                {
                    ExtractionStatus = $"❌ Error: {ex.Message}";
                }

                this.RaisePropertyChanged(nameof(IsExtractionPending));
                return;
            }
        }

        // No mods deployed - proceed with simple extraction flow
        await ForceExtractDataSimpleAsync();
    }

    /// <summary>
    /// Simple extraction flow when no mods are deployed.
    /// </summary>
    private async Task ForceExtractDataSimpleAsync()
    {
        var installer = new ModLoaderInstaller(GameInstallPath);

        // Ensure DataExtractor mod is deployed (force extraction since user explicitly requested it)
        ExtractionStatus = "Deploying DataExtractor mod...";
        if (!installer.IsDataExtractorInstalled())
        {
            var installed = await installer.InstallDataExtractorAsync(s => ExtractionStatus = s, forceExtraction: true);
            if (!installed)
                return;
        }
        else
        {
            // Update to latest version
            await installer.InstallDataExtractorAsync(s => ExtractionStatus = s, forceExtraction: true);
        }

        // Delete fingerprint to force re-extraction
        var fingerprintPath = Path.Combine(GameInstallPath, "UserData", "ExtractedData", "_extraction_fingerprint.txt");
        if (File.Exists(fingerprintPath))
        {
            try
            {
                File.Delete(fingerprintPath);
            }
            catch { }
        }

        // Write the force extraction flag file (in case InstallDataExtractorAsync didn't already)
        installer.WriteForceExtractionFlag();

        ExtractionStatus = "⏳ Extraction pending - will run on next game launch";
        this.RaisePropertyChanged(nameof(IsExtractionPending));
    }

    /// <summary>
    /// Check for pending redeploy state from a previous session.
    /// If extraction was interrupted, offer to complete the redeploy.
    /// </summary>
    private async Task CheckPendingRedeployAsync()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
            return;

        var pendingState = PendingRedeployState.LoadFrom(GameInstallPath);
        if (pendingState == null || !pendingState.RedeployPending)
            return;

        // Check if extraction completed while we were away
        if (pendingState.IsExtractionComplete(GameInstallPath))
        {
            ExtractionStatus = "Previous extraction complete - ready to redeploy mods";

            // Request recovery dialog from the View
            var args = new RecoveryDialogRequestEventArgs(pendingState, _deployManager);
            RecoveryDialogRequested?.Invoke(this, args);

            try
            {
                var success = await args.Result.Task;
                if (success)
                {
                    ExtractionStatus = "✓ Mods redeployed successfully";
                }
                else
                {
                    ExtractionStatus = "Redeploy was cancelled";
                }
            }
            catch (Exception ex)
            {
                ExtractionStatus = $"❌ Redeploy error: {ex.Message}";
            }
        }
        else
        {
            // Extraction didn't complete - the pending state exists but fingerprint is old
            ExtractionStatus = "⚠ Previous extraction was interrupted. Click 'Force Extract Data' to retry.";
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
            // Use the component system to check for updates (same source as Setup screen)
            var statuses = await ComponentManager.Instance.GetComponentStatusAsync(forceRemoteFetch: true);
            var modkitStatus = statuses.FirstOrDefault(s => s.Name == "Modkit");

            if (modkitStatus == null)
            {
                AppUpdateStatus = "You're up to date";
                return;
            }

            var hasUpdate = modkitStatus.State == ComponentState.Outdated;
            LatestAppVersion = modkitStatus.LatestVersion;
            HasAppUpdate = hasUpdate;
            AppDownloadUrl = $"https://github.com/{AppGitHubOwner}/{AppGitHubRepo}/releases/latest";

            if (hasUpdate)
            {
                AppUpdateStatus = $"Update available: v{modkitStatus.LatestVersion}";
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

    /// <summary>
    /// Start the update flow by showing the setup screen.
    /// This allows updating the app and components.
    /// </summary>
    private async Task StartUpdateFlowAsync()
    {
        // Check if mods are deployed - undeploy them first
        var deployState = _deployManager.GetDeployState();
        var deployedModpacks = deployState.DeployedModpacks
            .Select(m => m.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        if (deployedModpacks.Count > 0)
        {
            AppUpdateStatus = $"Undeploying {deployedModpacks.Count} mod(s) before update...";
            ModkitLog.Info($"[ToolSettingsViewModel] Undeploying mods before update: {string.Join(", ", deployedModpacks)}");

            try
            {
                var undeployResult = await _deployManager.UndeployAllAsync(
                    new Progress<string>(msg => AppUpdateStatus = msg));

                if (!undeployResult.Success)
                {
                    AppUpdateStatus = $"Failed to undeploy mods: {undeployResult.Message}";
                    ModkitLog.Error($"[ToolSettingsViewModel] Undeploy failed: {undeployResult.Message}");
                    return;
                }

                ModkitLog.Info("[ToolSettingsViewModel] Mods undeployed successfully");
            }
            catch (Exception ex)
            {
                AppUpdateStatus = $"Failed to undeploy mods: {ex.Message}";
                ModkitLog.Error($"[ToolSettingsViewModel] Undeploy error: {ex}");
                return;
            }
        }

        AppUpdateStatus = "Opening update wizard...";

        var args = new UpdateFlowRequestEventArgs();
        UpdateFlowRequested?.Invoke(this, args);

        try
        {
            var success = await args.Result.Task;
            if (success)
            {
                // Clean up version-specific cached data after successful update
                await CleanupAfterUpdateAsync();

                if (deployedModpacks.Count > 0)
                {
                    AppUpdateStatus = $"Update complete - redeploy your {deployedModpacks.Count} mod(s) when ready";
                }
                else
                {
                    AppUpdateStatus = "Update complete";
                }
                HasAppUpdate = false;

                // Refresh component versions and validation
                LoadDependencyVersions();
                ValidateExtractedData();
                UpdateCacheStatus();
            }
            else
            {
                if (deployedModpacks.Count > 0)
                {
                    AppUpdateStatus = $"Update cancelled - redeploy your {deployedModpacks.Count} mod(s) to restore";
                }
                else
                {
                    AppUpdateStatus = "Update cancelled";
                }
            }

            // Notify that modpacks need to be refreshed (mods were undeployed)
            if (deployedModpacks.Count > 0)
            {
                ModpacksNeedRefresh?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            AppUpdateStatus = $"Update failed: {ex.Message}";
            ModkitLog.Error($"[ToolSettingsViewModel] Update flow failed: {ex}");

            // Still refresh modpacks if we undeployed them
            if (deployedModpacks.Count > 0)
            {
                ModpacksNeedRefresh?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Clean up cached data that may be incompatible between versions.
    /// Mirrors the "Clean Redeploy" functionality in LoaderSettingsViewModel.
    /// </summary>
    private async Task CleanupAfterUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
            return;

        ModkitLog.Info("[ToolSettingsViewModel] Cleaning up after update...");

        try
        {
            var installer = new ModLoaderInstaller(GameInstallPath);

            // Clean Mods and UserLibs directories (same as Clean Redeploy)
            AppUpdateStatus = "Cleaning mod directories...";
            await installer.CleanModsDirectoryAsync(s => AppUpdateStatus = s);

            // Clear extraction cache (the data format may have changed)
            AppUpdateStatus = "Clearing extraction cache...";
            var cachePath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache");
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
                ModkitLog.Info("[ToolSettingsViewModel] Cleared extraction cache");
            }

            // Remove extraction fingerprint to force re-extraction
            var fingerprintPath = Path.Combine(GameInstallPath, "UserData", "ExtractedData", "_extraction_fingerprint.txt");
            if (File.Exists(fingerprintPath))
            {
                File.Delete(fingerprintPath);
                ModkitLog.Info("[ToolSettingsViewModel] Removed extraction fingerprint");
            }

            // Reinstall all required components (MelonLoader, DataExtractor, ModpackLoader)
            AppUpdateStatus = "Reinstalling mod loader components...";
            if (!await installer.InstallAllRequiredAsync(s => AppUpdateStatus = s))
            {
                ModkitLog.Warn("[ToolSettingsViewModel] Some components failed to install");
            }

            // Set the force extraction flag so next game launch extracts fresh data
            installer.WriteForceExtractionFlag();
            ModkitLog.Info("[ToolSettingsViewModel] Set force extraction flag");
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ToolSettingsViewModel] Cleanup after update had errors: {ex.Message}");
            // Don't fail the update if cleanup fails - just warn
        }
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

    /// <summary>
    /// Refresh channel information after a channel change.
    /// Checks for updates on the new channel.
    /// </summary>
    private async Task RefreshChannelInfoAsync()
    {
        ChannelStatusMessage = "Checking for updates...";

        try
        {
            // Force refresh to get info from the new channel
            await CheckForAppUpdateAsync();

            var channel = AppSettings.Instance.UpdateChannel;
            var currentVersion = ModkitVersion.MelonVersion;

            if (HasAppUpdate)
            {
                ChannelStatusMessage = $"Current: v{currentVersion} ({(channel == "beta" ? "Beta" : "Stable")}) → v{LatestAppVersion} available";
            }
            else
            {
                ChannelStatusMessage = $"Current: v{currentVersion} ({(channel == "beta" ? "Beta" : "Stable")}) - up to date";
            }
        }
        catch (Exception ex)
        {
            ChannelStatusMessage = $"Error checking channel: {ex.Message}";
            ModkitLog.Warn($"[ToolSettingsViewModel] Channel info refresh failed: {ex.Message}");
        }
    }
}
