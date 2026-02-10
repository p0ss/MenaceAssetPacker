using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Models;
using System;

namespace Menace.Modkit.App.Controls;

/// <summary>
/// Helper for building consistent search result UI components across views.
/// </summary>
public static class SearchPanelBuilder
{
    public enum SortOption { Relevance, NameAsc, NameDesc, PathAsc, PathDesc }

    /// <summary>
    /// Creates a visual control for a single search result item.
    /// </summary>
    public static Control CreateSearchResultControl(SearchResultItem item)
    {
        var container = new StackPanel { Spacing = 2, Margin = new Thickness(8, 6) };

        // Breadcrumb row
        var breadcrumb = new TextBlock
        {
            Text = item.Breadcrumb,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        container.Children.Add(breadcrumb);

        // Name row with type indicator
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var name = new TextBlock
        {
            Text = item.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        nameRow.Children.Add(name);

        if (!string.IsNullOrEmpty(item.TypeIndicator))
        {
            var typeBadge = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#252525")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = item.TypeIndicator,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.Parse("#888888"))
                }
            };
            nameRow.Children.Add(typeBadge);
        }
        container.Children.Add(nameRow);

        // Snippet row (if present)
        if (!string.IsNullOrEmpty(item.Snippet))
        {
            var snippet = new TextBlock
            {
                Text = item.Snippet,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 2,
                TextWrapping = TextWrapping.Wrap
            };
            container.Children.Add(snippet);
        }

        return container;
    }

    /// <summary>
    /// Creates the sort options panel that replaces Expand/Collapse when searching.
    /// </summary>
    public static Control CreateSortPanel(Action<SortOption> onSortChanged, SortOption current = SortOption.Relevance)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 12)
        };

        var label = new TextBlock
        {
            Text = "Sort:",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        panel.Children.Add(label);

        var combo = new ComboBox
        {
            FontSize = 11,
            MinWidth = 100
        };
        combo.Items.Add("Relevance");
        combo.Items.Add("Name A-Z");
        combo.Items.Add("Name Z-A");
        combo.Items.Add("Path A-Z");
        combo.Items.Add("Path Z-A");
        combo.SelectedIndex = (int)current;
        combo.SelectionChanged += (s, e) =>
        {
            if (combo.SelectedIndex >= 0)
                onSortChanged((SortOption)combo.SelectedIndex);
        };
        combo.Classes.Add("input");
        panel.Children.Add(combo);

        return panel;
    }
}
