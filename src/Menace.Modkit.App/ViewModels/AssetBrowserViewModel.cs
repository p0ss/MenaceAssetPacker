using System.Collections.ObjectModel;
using ReactiveUI;
using System.Linq;

namespace Menace.Modkit.App.ViewModels;

public sealed class AssetBrowserViewModel : ViewModelBase
{
  private readonly ObservableCollection<AssetBrowserItemViewModel> _allItems = new();

  public AssetBrowserViewModel()
  {
    Items = new ObservableCollection<AssetBrowserItemViewModel>();

    // Placeholder data until typetree manifest wiring is complete.
    _allItems.Add(new AssetBrowserItemViewModel(
      "resources.assets.json",
      "Menace_Data/resources.assets",
      "2022.3.20f1",
      182,
      45));

    _allItems.Add(new AssetBrowserItemViewModel(
      "globalgamemanagers.json",
      "Menace_Data/globalgamemanagers",
      "2022.3.20f1",
      96,
      12));

    _allItems.Add(new AssetBrowserItemViewModel(
      "level1.json",
      "Menace_Data/level1",
      "2022.3.20f1",
      151,
      61));

    ApplyFilter();
  }

  public ObservableCollection<AssetBrowserItemViewModel> Items { get; }

  private string _searchText = string.Empty;
  public string SearchText
  {
    get => _searchText;
    set
    {
      if (_searchText != value)
      {
        this.RaiseAndSetIfChanged(ref _searchText, value);
        ApplyFilter();
      }
    }
  }

  private void ApplyFilter()
  {
    Items.Clear();

    var filter = SearchText?.Trim() ?? string.Empty;
    IEnumerable<AssetBrowserItemViewModel> filtered = string.IsNullOrEmpty(filter)
      ? _allItems
      : _allItems.Where(item =>
          item.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
          item.SourcePath.Contains(filter, StringComparison.OrdinalIgnoreCase));

    foreach (var item in filtered)
    {
      Items.Add(item);
    }
  }
}

public sealed class AssetBrowserItemViewModel : ViewModelBase
{
  public AssetBrowserItemViewModel(string fileName, string sourcePath, string unityVersion, int typeCount, int referenceTypeCount)
  {
    FileName = fileName;
    SourcePath = sourcePath;
    UnityVersion = unityVersion;
    TypeCount = typeCount;
    ReferenceTypeCount = referenceTypeCount;
  }

  public string FileName { get; }

  public string SourcePath { get; }

  public string UnityVersion { get; }

  public int TypeCount { get; }

  public int ReferenceTypeCount { get; }
}
