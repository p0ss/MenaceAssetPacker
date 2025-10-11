using ReactiveUI;
using System.Reactive;

namespace Menace.Modkit.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
  public MainViewModel(System.IServiceProvider serviceProvider)
  {
    AssetBrowser = new AssetBrowserViewModel();
    StatsEditor = new StatsEditorViewModel();
    Modpacks = new ModpacksViewModel();
    Settings = new SettingsViewModel(serviceProvider);

    _selectedViewModel = Modpacks;
    _currentSectionTitle = "Modpacks";

    ShowAssetBrowser = ReactiveCommand.Create(() => Navigate(AssetBrowser, "Asset Browser"));
    ShowStatsEditor = ReactiveCommand.Create(() => Navigate(StatsEditor, "Stats Editor"));
    ShowModpacks = ReactiveCommand.Create(() => Navigate(Modpacks, "Modpacks"));
    ShowSettings = ReactiveCommand.Create(() => Navigate(Settings, "Settings"));

    Navigate(Modpacks, "Modpacks");
  }

  public AssetBrowserViewModel AssetBrowser { get; }

  public StatsEditorViewModel StatsEditor { get; }

  public ModpacksViewModel Modpacks { get; }

  public SettingsViewModel Settings { get; }

  private ViewModelBase _selectedViewModel;
  public ViewModelBase SelectedViewModel
  {
    get => _selectedViewModel;
    set => this.RaiseAndSetIfChanged(ref _selectedViewModel, value);
  }

  public ReactiveCommand<Unit, Unit> ShowAssetBrowser { get; }

  public ReactiveCommand<Unit, Unit> ShowStatsEditor { get; }

  public ReactiveCommand<Unit, Unit> ShowModpacks { get; }

  public ReactiveCommand<Unit, Unit> ShowSettings { get; }

  private string _currentSectionTitle;
  public string CurrentSectionTitle
  {
    get => _currentSectionTitle;
    private set => this.RaiseAndSetIfChanged(ref _currentSectionTitle, value);
  }

  private void Navigate(ViewModelBase target, string title)
  {
    SelectedViewModel = target;
    CurrentSectionTitle = title;
  }
}
