using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Simple markdown renderer that converts markdown text to Avalonia controls.
/// Supports: headers, bold, italic, code, code blocks, lists, links.
/// </summary>
public static class SimpleMarkdownRenderer
{
    /// <summary>
    /// Callback for handling internal document navigation (relative .md links).
    /// </summary>
    public static Action<string>? OnNavigateToDocument { get; set; }

    private static readonly SolidColorBrush TextColor = new(Color.Parse("#E0E0E0"));
    private static readonly SolidColorBrush MutedColor = new(Color.Parse("#888888"));
    private static readonly SolidColorBrush CodeBgColor = new(Color.Parse("#2D2D2D"));
    private static readonly SolidColorBrush LinkColor = new(Color.Parse("#6DB3F2"));
    private static readonly SolidColorBrush HeaderColor = new(Color.Parse("#FFFFFF"));

    public static Control Render(string markdown)
    {
        var stack = new StackPanel { Spacing = 12 };

        if (string.IsNullOrEmpty(markdown))
            return stack;

        var lines = markdown.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Code block
            if (trimmed.StartsWith("```"))
            {
                var codeLines = new List<string>();
                i++; // Skip opening ```
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }
                i++; // Skip closing ```
                stack.Children.Add(CreateCodeBlock(string.Join("\n", codeLines)));
                continue;
            }

            // Header
            if (trimmed.StartsWith("#"))
            {
                var level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;
                var text = trimmed.Substring(level).Trim();
                stack.Children.Add(CreateHeader(text, level));
                i++;
                continue;
            }

            // Horizontal rule
            if (trimmed.StartsWith("---") || trimmed.StartsWith("***") || trimmed.StartsWith("___"))
            {
                stack.Children.Add(new Border
                {
                    Height = 1,
                    Background = MutedColor,
                    Margin = new Thickness(0, 8)
                });
                i++;
                continue;
            }

            // Unordered list
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                var listItems = new List<string>();
                while (i < lines.Length)
                {
                    var listLine = lines[i].TrimStart();
                    if (listLine.StartsWith("- ") || listLine.StartsWith("* "))
                    {
                        listItems.Add(listLine.Substring(2));
                        i++;
                    }
                    else break;
                }
                stack.Children.Add(CreateList(listItems));
                continue;
            }

            // Ordered list
            if (Regex.IsMatch(trimmed, @"^\d+\.\s"))
            {
                var listItems = new List<string>();
                while (i < lines.Length)
                {
                    var listLine = lines[i].TrimStart();
                    var match = Regex.Match(listLine, @"^\d+\.\s(.*)");
                    if (match.Success)
                    {
                        listItems.Add(match.Groups[1].Value);
                        i++;
                    }
                    else break;
                }
                stack.Children.Add(CreateOrderedList(listItems));
                continue;
            }

            // Blockquote
            if (trimmed.StartsWith("> "))
            {
                var quoteLines = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith("> "))
                {
                    quoteLines.Add(lines[i].TrimStart().Substring(2));
                    i++;
                }
                stack.Children.Add(CreateBlockquote(string.Join("\n", quoteLines)));
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Regular paragraph - collect consecutive non-empty lines
            var paragraphLines = new List<string>();
            while (i < lines.Length)
            {
                var pLine = lines[i];
                var pTrimmed = pLine.TrimStart();

                // Stop at block-level elements
                if (string.IsNullOrWhiteSpace(pLine) ||
                    pTrimmed.StartsWith("#") ||
                    pTrimmed.StartsWith("```") ||
                    pTrimmed.StartsWith("- ") ||
                    pTrimmed.StartsWith("* ") ||
                    pTrimmed.StartsWith("> ") ||
                    Regex.IsMatch(pTrimmed, @"^\d+\.\s"))
                    break;

                paragraphLines.Add(pLine);
                i++;
            }

            if (paragraphLines.Count > 0)
            {
                stack.Children.Add(CreateParagraph(string.Join(" ", paragraphLines)));
            }
        }

        return stack;
    }

    private static Control CreateHeader(string text, int level)
    {
        var fontSize = level switch
        {
            1 => 24,
            2 => 20,
            3 => 16,
            _ => 14
        };

        // Check if header contains a link
        var linkMatch = Regex.Match(text, @"\[(.+?)\]\((.+?)\)");
        if (linkMatch.Success)
        {
            var linkText = linkMatch.Groups[1].Value;
            var linkUrl = linkMatch.Groups[2].Value;

            var link = new TextBlock
            {
                Text = linkText,
                FontSize = fontSize,
                FontWeight = FontWeight.Bold,
                Foreground = LinkColor,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand),
                Margin = new Thickness(0, level == 1 ? 8 : 4, 0, 4),
                TextWrapping = TextWrapping.Wrap
            };

            link.PointerPressed += (_, _) => HandleLinkClick(linkUrl);
            link.PointerEntered += (_, _) => link.Opacity = 0.8;
            link.PointerExited += (_, _) => link.Opacity = 1.0;

            return link;
        }

        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            Foreground = HeaderColor,
            Margin = new Thickness(0, level == 1 ? 8 : 4, 0, 4),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Control CreateParagraph(string text)
    {
        return CreateFormattedTextBlock(text);
    }

    private static Control CreateFormattedTextBlock(string text)
    {
        // Check if text contains links - if so, use WrapPanel approach
        if (text.Contains("[") && text.Contains("]("))
        {
            return CreateFormattedWrapPanel(text);
        }

        var textBlock = new SelectableTextBlock
        {
            Foreground = TextColor,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };

        // Parse inline formatting and build inlines
        var inlines = ParseInlineFormatting(text);
        foreach (var inline in inlines)
        {
            textBlock.Inlines?.Add(inline);
        }

        return textBlock;
    }

    private static Control CreateFormattedWrapPanel(string text)
    {
        var panel = new WrapPanel();

        // Pattern to match links and other inline elements
        var linkPattern = @"\[(.+?)\]\((.+?)\)";
        var lastIndex = 0;

        foreach (Match match in Regex.Matches(text, linkPattern))
        {
            // Add text before link
            if (match.Index > lastIndex)
            {
                var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                panel.Children.Add(CreateInlineTextBlock(beforeText));
            }

            // Create clickable link
            var linkText = match.Groups[1].Value;
            var linkUrl = match.Groups[2].Value;
            panel.Children.Add(CreateClickableLink(linkText, linkUrl));

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            panel.Children.Add(CreateInlineTextBlock(text.Substring(lastIndex)));
        }

        return panel;
    }

    private static Control CreateInlineTextBlock(string text)
    {
        var textBlock = new SelectableTextBlock
        {
            Foreground = TextColor,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var inlines = ParseInlineFormattingNoLinks(text);
        foreach (var inline in inlines)
        {
            textBlock.Inlines?.Add(inline);
        }

        return textBlock;
    }

    private static Control CreateClickableLink(string text, string url)
    {
        var link = new TextBlock
        {
            Text = text,
            Foreground = LinkColor,
            FontSize = 13,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0)
        };

        link.PointerPressed += (_, _) =>
        {
            HandleLinkClick(url);
        };

        link.PointerEntered += (_, _) =>
        {
            link.Opacity = 0.8;
        };

        link.PointerExited += (_, _) =>
        {
            link.Opacity = 1.0;
        };

        return link;
    }

    private static void HandleLinkClick(string url)
    {
        try
        {
            // External URL - open in browser
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            // Internal markdown link - navigate within docs
            else if (url.EndsWith(".md") || !url.Contains("://"))
            {
                OnNavigateToDocument?.Invoke(url);
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[SimpleMarkdownRenderer] Failed to open link: {url} - {ex.Message}");
        }
    }

    private static List<Avalonia.Controls.Documents.Inline> ParseInlineFormattingNoLinks(string text)
    {
        var inlines = new List<Avalonia.Controls.Documents.Inline>();

        // Regex patterns for inline elements (no links)
        var pattern = @"(\*\*\*.+?\*\*\*|\*\*.+?\*\*|\*.+?\*|`.+?`)";

        var lastIndex = 0;
        foreach (Match match in Regex.Matches(text, pattern))
        {
            // Add text before match
            if (match.Index > lastIndex)
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(text.Substring(lastIndex, match.Index - lastIndex)));
            }

            var value = match.Value;

            // Bold + Italic
            if (value.StartsWith("***") && value.EndsWith("***"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(3, value.Length - 6))
                {
                    FontWeight = FontWeight.Bold,
                    FontStyle = FontStyle.Italic
                });
            }
            // Bold
            else if (value.StartsWith("**") && value.EndsWith("**"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(2, value.Length - 4))
                {
                    FontWeight = FontWeight.Bold
                });
            }
            // Italic
            else if (value.StartsWith("*") && value.EndsWith("*"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(1, value.Length - 2))
                {
                    FontStyle = FontStyle.Italic
                });
            }
            // Inline code
            else if (value.StartsWith("`") && value.EndsWith("`"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(1, value.Length - 2))
                {
                    FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                    Background = CodeBgColor
                });
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            inlines.Add(new Avalonia.Controls.Documents.Run(text.Substring(lastIndex)));
        }

        // If no inlines were added, add the whole text
        if (inlines.Count == 0)
        {
            inlines.Add(new Avalonia.Controls.Documents.Run(text));
        }

        return inlines;
    }

    private static List<Avalonia.Controls.Documents.Inline> ParseInlineFormatting(string text)
    {
        var inlines = new List<Avalonia.Controls.Documents.Inline>();

        // Regex patterns for inline elements
        // Order matters: check longer patterns first
        var pattern = @"(\*\*\*.+?\*\*\*|\*\*.+?\*\*|\*.+?\*|`.+?`|\[.+?\]\(.+?\))";

        var lastIndex = 0;
        foreach (Match match in Regex.Matches(text, pattern))
        {
            // Add text before match
            if (match.Index > lastIndex)
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(text.Substring(lastIndex, match.Index - lastIndex)));
            }

            var value = match.Value;

            // Bold + Italic
            if (value.StartsWith("***") && value.EndsWith("***"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(3, value.Length - 6))
                {
                    FontWeight = FontWeight.Bold,
                    FontStyle = FontStyle.Italic
                });
            }
            // Bold
            else if (value.StartsWith("**") && value.EndsWith("**"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(2, value.Length - 4))
                {
                    FontWeight = FontWeight.Bold
                });
            }
            // Italic
            else if (value.StartsWith("*") && value.EndsWith("*"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(1, value.Length - 2))
                {
                    FontStyle = FontStyle.Italic
                });
            }
            // Inline code
            else if (value.StartsWith("`") && value.EndsWith("`"))
            {
                inlines.Add(new Avalonia.Controls.Documents.Run(value.Substring(1, value.Length - 2))
                {
                    FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                    Background = CodeBgColor
                });
            }
            // Link - render as styled text (click handling via WrapPanel path)
            else if (value.StartsWith("["))
            {
                var linkMatch = Regex.Match(value, @"\[(.+?)\]\((.+?)\)");
                if (linkMatch.Success)
                {
                    var linkText = linkMatch.Groups[1].Value;
                    inlines.Add(new Avalonia.Controls.Documents.Run(linkText)
                    {
                        Foreground = LinkColor,
                        TextDecorations = Avalonia.Media.TextDecorations.Underline
                    });
                }
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            inlines.Add(new Avalonia.Controls.Documents.Run(text.Substring(lastIndex)));
        }

        // If no inlines were added, add the whole text
        if (inlines.Count == 0)
        {
            inlines.Add(new Avalonia.Controls.Documents.Run(text));
        }

        return inlines;
    }

    private static Control CreateCodeBlock(string code)
    {
        return new Border
        {
            Background = CodeBgColor,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8),
            Child = new SelectableTextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas, Menlo, monospace"),
                FontSize = 12,
                Foreground = TextColor,
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    private static Control CreateList(List<string> items)
    {
        var stack = new StackPanel { Spacing = 4 };
        foreach (var item in items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = "\u2022",
                Foreground = MutedColor,
                FontSize = 13,
                Width = 12
            });
            row.Children.Add(CreateFormattedTextBlock(item));
            stack.Children.Add(row);
        }
        return stack;
    }

    private static Control CreateOrderedList(List<string> items)
    {
        var stack = new StackPanel { Spacing = 4 };
        for (var i = 0; i < items.Count; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock
            {
                Text = $"{i + 1}.",
                Foreground = MutedColor,
                FontSize = 13,
                Width = 20
            });
            row.Children.Add(CreateFormattedTextBlock(items[i]));
            stack.Children.Add(row);
        }
        return stack;
    }

    private static Control CreateBlockquote(string text)
    {
        return new Border
        {
            BorderBrush = MutedColor,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 4),
            Child = new TextBlock
            {
                Text = text,
                Foreground = MutedColor,
                FontSize = 13,
                FontStyle = FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }
}
