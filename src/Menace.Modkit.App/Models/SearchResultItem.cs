namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents a single search result item for display in the search results list.
/// </summary>
public class SearchResultItem
{
    /// <summary>Folder path displayed as breadcrumb (e.g., "Units / Enemy / Boss")</summary>
    public string Breadcrumb { get; init; } = string.Empty;

    /// <summary>Primary name displayed prominently</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Context snippet (description, first lines, field values)</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>Search relevance score for sorting</summary>
    public int Score { get; init; }

    /// <summary>Reference to underlying node for selection</summary>
    public object SourceNode { get; init; } = null!;

    /// <summary>File extension or type indicator (e.g., ".cs", ".md", "template")</summary>
    public string TypeIndicator { get; init; } = string.Empty;
}
