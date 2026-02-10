using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Models;
using System.Collections.ObjectModel;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// Interface for view models that support enhanced search with results list display.
/// </summary>
public interface ISearchableViewModel
{
    /// <summary>Current search text entered by user.</summary>
    string SearchText { get; set; }

    /// <summary>True when SearchText is not empty, triggering search results mode.</summary>
    bool IsSearching { get; }

    /// <summary>Collection of search results to display when IsSearching is true.</summary>
    ObservableCollection<SearchResultItem> SearchResults { get; }

    /// <summary>Current sort option for search results.</summary>
    SearchPanelBuilder.SortOption CurrentSortOption { get; set; }

    /// <summary>Available section filters (top-level folders).</summary>
    ObservableCollection<string> SectionFilters { get; }

    /// <summary>Currently selected section filter, or null/empty for all sections.</summary>
    string? SelectedSectionFilter { get; set; }

    /// <summary>Called when user clicks on a search result to select it.</summary>
    void SelectSearchResult(SearchResultItem item);

    /// <summary>Called when user double-clicks a search result to select it and exit search mode.</summary>
    void SelectAndExitSearch(SearchResultItem item);

    /// <summary>Expands tree to show and focus the currently selected item.</summary>
    void FocusSelectedInTree();
}
