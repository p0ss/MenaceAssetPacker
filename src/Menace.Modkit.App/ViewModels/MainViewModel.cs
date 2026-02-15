using ReactiveUI;
using System;
using System.Reactive;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// Navigation section at the top level.
/// </summary>
public enum NavigationSection
{
    Home,
    ModLoader,
    ModdingTools
}

public sealed class MainViewModel : ViewModelBase
{
    public MainViewModel(IServiceProvider serviceProvider)
    {
        // Initialize all view models
        Home = new HomeViewModel();
        Modpacks = new ModpacksViewModel();
        SaveEditor = new SaveEditorViewModel();
        LoaderSettings = new LoaderSettingsViewModel();
        StatsEditor = new StatsEditorViewModel();
        AssetBrowser = new AssetBrowserViewModel();
        CodeEditor = new CodeEditorViewModel();
        Docs = new DocsViewModel();
        ToolSettings = new ToolSettingsViewModel(serviceProvider);

        _selectedViewModel = Home;
        _currentSection = NavigationSection.Home;
        _currentSubSection = "";

        // Wire up cross-tab navigation: modpacks → stats editor
        Modpacks.NavigateToStatsEntry = (modpackName, templateType, instanceName) =>
        {
            NavigateToModdingTools();
            NavigateTo(StatsEditor, "Data");
            StatsEditor.NavigateToEntry(modpackName, templateType, instanceName);
        };

        // Wire up cross-tab navigation: modpacks → asset browser
        Modpacks.NavigateToAssetEntry = (modpackName, assetRelativePath) =>
        {
            NavigateToModdingTools();
            NavigateTo(AssetBrowser, "Assets");
            AssetBrowser.NavigateToAssetEntry(modpackName, assetRelativePath);
        };

        // Wire up cross-tab navigation: asset browser → stats editor
        AssetBrowser.NavigateToTemplate += (modpackName, templateType, instanceName) =>
        {
            NavigateTo(StatsEditor, "Data");
            StatsEditor.NavigateToEntry(modpackName ?? "", templateType, instanceName);
        };

        // Share the reference graph service from StatsEditor to AssetBrowser
        AssetBrowser.SetReferenceGraphService(StatsEditor.ReferenceGraphService);

        // Wire up cross-tab navigation: code editor → docs (Lua scripting guide)
        CodeEditor.NavigateToLuaDocs = () =>
        {
            NavigateTo(Docs, "Docs");
            Docs.NavigateToDocByName("lua-scripting");
        };
    }

    // Home
    public HomeViewModel Home { get; }

    // Mod Loader section
    public ModpacksViewModel Modpacks { get; }
    public SaveEditorViewModel SaveEditor { get; }
    public LoaderSettingsViewModel LoaderSettings { get; }

    // Modding Tools section
    public StatsEditorViewModel StatsEditor { get; }
    public AssetBrowserViewModel AssetBrowser { get; }
    public CodeEditorViewModel CodeEditor { get; }
    public DocsViewModel Docs { get; }
    public ToolSettingsViewModel ToolSettings { get; }

    private ViewModelBase _selectedViewModel;
    public ViewModelBase SelectedViewModel
    {
        get => _selectedViewModel;
        set => this.RaiseAndSetIfChanged(ref _selectedViewModel, value);
    }

    private NavigationSection _currentSection;
    public NavigationSection CurrentSection
    {
        get => _currentSection;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentSection, value);
            this.RaisePropertyChanged(nameof(IsHome));
            this.RaisePropertyChanged(nameof(IsModLoader));
            this.RaisePropertyChanged(nameof(IsModdingTools));
        }
    }

    private string _currentSubSection;
    public string CurrentSubSection
    {
        get => _currentSubSection;
        private set => this.RaiseAndSetIfChanged(ref _currentSubSection, value);
    }

    public bool IsHome => CurrentSection == NavigationSection.Home;
    public bool IsModLoader => CurrentSection == NavigationSection.ModLoader;
    public bool IsModdingTools => CurrentSection == NavigationSection.ModdingTools;

    /// <summary>
    /// Navigate to the Home screen.
    /// </summary>
    public void NavigateToHome()
    {
        CurrentSection = NavigationSection.Home;
        CurrentSubSection = "";
        SelectedViewModel = Home;
    }

    /// <summary>
    /// Navigate to the Mod Loader section (defaults to Load Order).
    /// </summary>
    public void NavigateToModLoader()
    {
        CurrentSection = NavigationSection.ModLoader;
        NavigateTo(Modpacks, "Load Order");
    }

    /// <summary>
    /// Navigate to the Modding Tools section (defaults to Data).
    /// Also marks the user as a modding tools user, enabling data extraction.
    /// </summary>
    public void NavigateToModdingTools()
    {
        // Mark user as interested in modding tools - this enables data extraction
        Services.AppSettings.Instance.SetHasUsedModdingTools(true);

        CurrentSection = NavigationSection.ModdingTools;
        NavigateTo(StatsEditor, "Data");
    }

    /// <summary>
    /// Navigate to a specific view within the current section.
    /// </summary>
    public void NavigateTo(ViewModelBase target, string subSection)
    {
        SelectedViewModel = target;
        CurrentSubSection = subSection;

        // Update section based on target
        if (target == Modpacks || target == SaveEditor || target == LoaderSettings)
        {
            CurrentSection = NavigationSection.ModLoader;
        }
        else if (target == StatsEditor || target == AssetBrowser || target == CodeEditor || target == Docs || target == ToolSettings)
        {
            CurrentSection = NavigationSection.ModdingTools;
        }
        else if (target == Home)
        {
            CurrentSection = NavigationSection.Home;
        }

        // Refresh stats patches when switching to modpacks tab
        if (target == Modpacks)
            Modpacks.SelectedModpack?.RefreshStatsPatches();
    }

    // Convenience methods for sub-section navigation
    // Mod Loader section
    public void NavigateToLoadOrder() => NavigateTo(Modpacks, "Load Order");
    public void NavigateToSaves() => NavigateTo(SaveEditor, "Saves");
    public void NavigateToLoaderSettings() => NavigateTo(LoaderSettings, "Settings");

    // Modding Tools section
    public void NavigateToData() => NavigateTo(StatsEditor, "Data");
    public void NavigateToAssets() => NavigateTo(AssetBrowser, "Assets");
    public void NavigateToCode() => NavigateTo(CodeEditor, "Code");
    public void NavigateToDocs() => NavigateTo(Docs, "Docs");
    public void NavigateToToolSettings() => NavigateTo(ToolSettings, "Settings");
}
