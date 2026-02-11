using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

// Import enums from EnvironmentChecker
using CheckStatus = Menace.Modkit.App.Services.CheckStatus;
using AutoFixAction = Menace.Modkit.App.Services.AutoFixAction;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Setup/update screen that shows component status and handles downloads.
/// </summary>
public class SetupView : UserControl
{
    public SetupView()
    {
        Content = BuildUI();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is SetupViewModel vm)
        {
            await vm.LoadComponentsAsync();
        }
    }

    private Control BuildUI()
    {
        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            Padding = new Thickness(48)
        };

        var mainStack = new StackPanel
        {
            MaxWidth = 700,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 24
        };

        // Header
        mainStack.Children.Add(BuildHeader());

        // Loading indicator
        var loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        loadingPanel.Children.Add(new ProgressBar
        {
            IsIndeterminate = true,
            Width = 200,
            Height = 4
        });
        loadingPanel.Children.Add(new TextBlock
        {
            Text = "Checking for updates...",
            Foreground = Brushes.White,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        loadingPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("IsLoading"));
        mainStack.Children.Add(loadingPanel);

        // Content (hidden while loading)
        var contentPanel = new StackPanel { Spacing = 24 };
        contentPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("!IsLoading"));

        // Environment Checks Section
        contentPanel.Children.Add(BuildEnvironmentSection());

        // Required Components Section
        contentPanel.Children.Add(BuildRequiredSection());

        // Optional Components Section (includes AI Assistant as accordion)
        contentPanel.Children.Add(BuildOptionalSection());

        // Fix Progress Section
        contentPanel.Children.Add(BuildFixProgressSection());

        // Action Buttons + Status
        contentPanel.Children.Add(BuildActionSection());

        mainStack.Children.Add(contentPanel);
        mainBorder.Child = mainStack;

        return new ScrollViewer
        {
            Content = mainBorder,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Control BuildHeader()
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Menace Modkit Setup",
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "The following components are needed to run the modkit.",
            FontSize = 14,
            Foreground = Brushes.White,
            Opacity = 0.7
        });

        return stack;
    }

    private Control BuildRequiredSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20)
        };

        var stack = new StackPanel { Spacing = 12 };

        // Section header - standardized style
        stack.Children.Add(BuildSectionHeader("REQUIRED COMPONENTS"));

        // Component list
        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("RequiredComponents"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ComponentStatusViewModel>(
            (component, _) => BuildComponentRow(component, isRequired: true), true);
        stack.Children.Add(itemsControl);

        border.Child = stack;
        return border;
    }

    private Control BuildOptionalSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20)
        };

        var stack = new StackPanel { Spacing = 12 };

        // Section header - standardized style
        stack.Children.Add(BuildSectionHeader("OPTIONAL ADD-ONS"));

        // Component list
        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("OptionalComponents"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ComponentStatusViewModel>(
            (component, _) => BuildComponentRow(component, isRequired: false), true);
        stack.Children.Add(itemsControl);

        // AI Assistant accordion (collapsed by default, shown when MCP server component is present)
        stack.Children.Add(BuildAiAssistantAccordion());

        border.Child = stack;
        return border;
    }

    private Control BuildSectionHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            LetterSpacing = 1
        };
    }

    private Control BuildComponentRow(ComponentStatusViewModel component, bool isRequired)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(0, 6)
        };

        // Column 0: Status indicator or checkbox
        if (isRequired)
        {
            var statusIcon = new TextBlock
            {
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            // Bind icon and color based on state - using blue for download (not destructive)
            statusIcon.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("State")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, string>(state => state switch
                {
                    ComponentState.UpToDate => "\u2713",      // Checkmark
                    ComponentState.Outdated => "\u2191",      // Up arrow
                    ComponentState.NotInstalled => "\u2193",  // Down arrow (download)
                    _ => "?"
                })
            });
            statusIcon.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("State")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
                {
                    ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#8ECDC8")),
                    ComponentState.Outdated => new SolidColorBrush(Color.Parse("#FFD700")),
                    ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#6B9FFF")), // Blue
                    _ => Brushes.White
                })
            });

            grid.Children.Add(statusIcon);
            Grid.SetColumn(statusIcon, 0);
        }
        else
        {
            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkbox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding("IsSelected")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
            // Disable if already installed
            checkbox.Bind(CheckBox.IsEnabledProperty, new Avalonia.Data.Binding("NeedsAction"));

            grid.Children.Add(checkbox);
            Grid.SetColumn(checkbox, 0);
        }

        // Column 1: Name and description
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        nameStack.Children.Add(new TextBlock
        {
            Text = component.Name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = $"v{component.LatestVersion}",
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center
        });
        infoStack.Children.Add(nameStack);

        infoStack.Children.Add(new TextBlock
        {
            Text = component.Description,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.6
        });

        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Column 2: Size
        var sizeText = new TextBlock
        {
            Text = component.DownloadSize,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0)
        };
        // Hide size if already installed
        sizeText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("NeedsAction"));
        grid.Children.Add(sizeText);
        Grid.SetColumn(sizeText, 2);

        // Column 3: Status badge
        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        var statusText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };

        // Bind badge appearance based on state - using blue for download (not destructive)
        statusBadge.Bind(Border.BackgroundProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
            {
                ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#1a3a2a")),
                ComponentState.Outdated => new SolidColorBrush(Color.Parse("#3a3a1a")),
                ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#1a2a3a")), // Blue, not red
                _ => Brushes.Transparent
            })
        });

        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, string>(state => state switch
            {
                ComponentState.UpToDate => "Installed",
                ComponentState.Outdated => "Update",
                ComponentState.NotInstalled => "Required",
                _ => ""
            })
        });

        statusText.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
            {
                ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#8ECDC8")),
                ComponentState.Outdated => new SolidColorBrush(Color.Parse("#FFD700")),
                ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#6B9FFF")), // Blue, not red
                _ => Brushes.White
            })
        });

        statusBadge.Child = statusText;
        grid.Children.Add(statusBadge);
        Grid.SetColumn(statusBadge, 3);

        return grid;
    }

    private Control BuildEnvironmentSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20)
        };

        var stack = new StackPanel { Spacing = 12 };

        // Section header - standardized style with status
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        headerStack.Children.Add(new TextBlock
        {
            Text = "ENVIRONMENT",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            LetterSpacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Status indicator
        var statusIcon = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        statusIcon.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("HasEnvironmentIssues")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(hasIssues =>
                hasIssues ? "(issues found)" : "(all good)")
        });
        statusIcon.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("HasEnvironmentFailures")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, IBrush>(hasFailed =>
                hasFailed ? new SolidColorBrush(Color.Parse("#FF6B6B"))
                          : new SolidColorBrush(Color.Parse("#8ECDC8")))
        });
        headerStack.Children.Add(statusIcon);
        Grid.SetColumn(headerStack, 0);
        headerGrid.Children.Add(headerStack);

        // Open Log button
        var logButton = new Button
        {
            Content = "View Log",
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        logButton.Classes.Add("secondary");
        logButton.Click += async (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
            {
                await vm.WriteDiagnosticReportAsync();
            }
        };
        Grid.SetColumn(logButton, 1);
        headerGrid.Children.Add(logButton);

        stack.Children.Add(headerGrid);

        // Environment check list
        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("EnvironmentChecks"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EnvironmentCheckViewModel>(
            (check, _) => BuildEnvironmentCheckRow(check), true);
        stack.Children.Add(itemsControl);

        border.Child = stack;
        return border;
    }

    private Control BuildAiAssistantAccordion()
    {
        var expander = new Expander
        {
            IsExpanded = false,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // Header row with MCP toggle
        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        headerGrid.Children.Add(new TextBlock
        {
            Text = "Modding Assistant",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(headerGrid.Children[0], 0);

        // Status indicator
        var statusText = new TextBlock
        {
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("HasAnyAiClient")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(hasClient =>
                hasClient ? "(client detected)" : "")
        });
        statusText.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("HasAnyAiClient")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, IBrush>(hasClient =>
                hasClient ? new SolidColorBrush(Color.Parse("#8ECDC8"))
                          : new SolidColorBrush(Color.Parse("#888888")))
        });
        headerGrid.Children.Add(statusText);
        Grid.SetColumn(statusText, 1);

        expander.Header = headerGrid;

        // Content panel
        var contentStack = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };

        // MCP toggle row
        var toggleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var toggleInfo = new StackPanel();
        toggleInfo.Children.Add(new TextBlock
        {
            Text = "MCP Server",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        toggleInfo.Children.Add(new TextBlock
        {
            Text = "Enables AI assistants (Claude Desktop, OpenCode, etc.) to interact with the modkit",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.6,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        toggleRow.Children.Add(toggleInfo);
        Grid.SetColumn(toggleInfo, 0);

        var mcpToggle = new ToggleSwitch
        {
            OnContent = "On",
            OffContent = "Off",
            VerticalAlignment = VerticalAlignment.Center
        };
        mcpToggle.Bind(ToggleSwitch.IsCheckedProperty, new Avalonia.Data.Binding("IsMcpEnabled")
        {
            Mode = Avalonia.Data.BindingMode.TwoWay
        });
        toggleRow.Children.Add(mcpToggle);
        Grid.SetColumn(mcpToggle, 1);

        contentStack.Children.Add(toggleRow);

        // Client list (only shown when MCP is enabled)
        var clientsPanel = new StackPanel { Spacing = 8 };
        clientsPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("IsMcpEnabled"));

        clientsPanel.Children.Add(new TextBlock
        {
            Text = "Detected Clients:",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.6,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("AiClients"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<AiClientViewModel>(
            (client, _) => BuildAiClientRow(client), true);
        clientsPanel.Children.Add(itemsControl);

        // Setup docs link
        var docsLink = new Button
        {
            Content = "View Setup Guide",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        docsLink.Classes.Add("secondary");
        docsLink.Click += (_, _) =>
        {
            var localDocsPath = Path.Combine(AppContext.BaseDirectory, "docs", "system-guide", "AI_ASSISTANT_SETUP.md");
            if (!File.Exists(localDocsPath))
            {
                localDocsPath = Path.Combine(Environment.CurrentDirectory, "docs", "system-guide", "AI_ASSISTANT_SETUP.md");
            }

            if (File.Exists(localDocsPath))
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(localDocsPath)
                    {
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch { }
            }
        };
        clientsPanel.Children.Add(docsLink);

        contentStack.Children.Add(clientsPanel);

        expander.Content = contentStack;
        return expander;
    }

    private Control BuildAiClientRow(AiClientViewModel client)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 6)
        };

        // Column 0: Status icon
        var statusIcon = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        if (!client.IsInstalled)
        {
            statusIcon.Text = "\u2014"; // Em dash (not detected)
            statusIcon.Foreground = new SolidColorBrush(Color.Parse("#666666"));
        }
        else if (client.IsConfigured)
        {
            statusIcon.Text = "\u2713"; // Checkmark
            statusIcon.Foreground = new SolidColorBrush(Color.Parse("#8ECDC8"));
        }
        else
        {
            statusIcon.Text = "\u26A0"; // Warning
            statusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFD700"));
        }

        grid.Children.Add(statusIcon);
        Grid.SetColumn(statusIcon, 0);

        // Column 1: Name and description
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        nameStack.Children.Add(new TextBlock
        {
            Text = client.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = client.IsInstalled ? Brushes.White : new SolidColorBrush(Color.Parse("#666666"))
        });

        if (client.IsInstalled)
        {
            var statusBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = client.IsConfigured
                    ? new SolidColorBrush(Color.Parse("#1a3a2a"))
                    : new SolidColorBrush(Color.Parse("#3a3a1a"))
            };
            statusBadge.Child = new TextBlock
            {
                Text = client.IsConfigured ? "Configured" : "Needs Setup",
                FontSize = 10,
                Foreground = client.IsConfigured
                    ? new SolidColorBrush(Color.Parse("#8ECDC8"))
                    : new SolidColorBrush(Color.Parse("#FFD700"))
            };
            nameStack.Children.Add(statusBadge);
        }

        infoStack.Children.Add(nameStack);

        infoStack.Children.Add(new TextBlock
        {
            Text = client.Description,
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = client.IsInstalled ? 0.6 : 0.4
        });

        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Column 2: Action button
        if (client.IsInstalled && !client.IsConfigured)
        {
            var configButton = new Button
            {
                Content = "Configure",
                FontSize = 11,
                Padding = new Thickness(8, 4)
            };
            configButton.Classes.Add("primary");
            configButton.Click += async (_, _) =>
            {
                await client.ConfigureAsync();
            };
            grid.Children.Add(configButton);
            Grid.SetColumn(configButton, 2);
        }
        else if (!client.IsInstalled && client.HasSetupDocs)
        {
            var installButton = new Button
            {
                Content = "Install",
                FontSize = 11,
                Padding = new Thickness(8, 4)
            };
            installButton.Classes.Add("secondary");
            installButton.Click += (_, _) =>
            {
                client.OpenSetupDocs();
            };
            grid.Children.Add(installButton);
            Grid.SetColumn(installButton, 2);
        }

        return grid;
    }

    private Control BuildEnvironmentCheckRow(EnvironmentCheckViewModel check)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 6)
        };

        // Column 0: Status icon
        var statusIcon = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        statusIcon.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Status")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<CheckStatus, string>(status => status switch
            {
                CheckStatus.Passed => "\u2713",   // Checkmark
                CheckStatus.Warning => "\u26A0",  // Warning triangle
                CheckStatus.Failed => "\u2717",   // X
                _ => "?"
            })
        });
        statusIcon.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("Status")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<CheckStatus, IBrush>(status => status switch
            {
                CheckStatus.Passed => new SolidColorBrush(Color.Parse("#8ECDC8")),
                CheckStatus.Warning => new SolidColorBrush(Color.Parse("#FFD700")),
                CheckStatus.Failed => new SolidColorBrush(Color.Parse("#FF6B6B")),
                _ => Brushes.White
            })
        });

        grid.Children.Add(statusIcon);
        Grid.SetColumn(statusIcon, 0);

        // Column 1: Name and description
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        nameStack.Children.Add(new TextBlock
        {
            Text = check.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = check.Description,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center
        });
        infoStack.Children.Add(nameStack);

        // Show fix instructions for issues
        if (check.HasIssue && !string.IsNullOrEmpty(check.FixInstructions))
        {
            var fixText = new TextBlock
            {
                Text = check.FixInstructions,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 400
            };
            infoStack.Children.Add(fixText);
        }

        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Column 2: Fix button(s)
        if (check.HasIssue)
        {
            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Auto-fix button
            if (check.CanAutoFix)
            {
                var fixButton = new Button
                {
                    Content = GetFixButtonText(check.AutoFixAction),
                    FontSize = 11,
                    Padding = new Thickness(8, 4)
                };
                fixButton.Classes.Add("primary");
                fixButton.Click += async (_, _) =>
                {
                    await check.ExecuteAutoFixAsync();
                };
                buttonStack.Children.Add(fixButton);
            }

            // URL button for external fixes
            if (check.HasFixUrl)
            {
                var urlButton = new Button
                {
                    Content = "Download",
                    FontSize = 11,
                    Padding = new Thickness(8, 4)
                };
                urlButton.Classes.Add("secondary");
                urlButton.Click += (_, _) =>
                {
                    check.OpenFixUrl();
                };
                buttonStack.Children.Add(urlButton);
            }

            if (buttonStack.Children.Count > 0)
            {
                grid.Children.Add(buttonStack);
                Grid.SetColumn(buttonStack, 2);
            }
        }

        return grid;
    }

    private static string GetFixButtonText(AutoFixAction action)
    {
        return action switch
        {
            AutoFixAction.InstallMelonLoader => "Install",
            AutoFixAction.LaunchGame => "Launch Game",
            AutoFixAction.InstallDataExtractor => "Install",
            AutoFixAction.InstallModpackLoader => "Install",
            _ => "Fix"
        };
    }

    private Control BuildFixProgressSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2a1a3a")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 8, 0, 0)
        };
        border.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("IsFixing"));

        var stack = new StackPanel { Spacing = 8 };

        stack.Children.Add(new TextBlock
        {
            Text = "Applying fix...",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4
        };
        stack.Children.Add(progressBar);

        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.7
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("FixStatus"));
        stack.Children.Add(statusText);

        border.Child = stack;
        return border;
    }

    private Control BuildActionSection()
    {
        var container = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 16, 0, 0)
        };

        // ==========================================
        // Button row
        // ==========================================
        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16
        };

        // Download button
        var downloadButton = new Button
        {
            FontSize = 14,
            Padding = new Thickness(24, 12),
            MinWidth = 200
        };
        downloadButton.Classes.Add("primary");

        var downloadPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var downloadIcon = new TextBlock
        {
            Text = "\u2193",
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
        downloadPanel.Children.Add(downloadIcon);

        var downloadText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        downloadText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TotalDownloadSize")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<string, string>(size =>
                string.IsNullOrEmpty(size) ? "Continue" : $"Download All ({size})")
        });
        downloadPanel.Children.Add(downloadText);

        downloadButton.Content = downloadPanel;
        downloadButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("CanDownload"));
        downloadButton.Click += async (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
            {
                if (vm.HasPendingDownloads)
                    await vm.DownloadAsync();
                else
                    vm.Continue();
            }
        };
        buttonStack.Children.Add(downloadButton);

        // Cancel button (shown during download)
        var cancelButton = new Button
        {
            Content = "Cancel",
            FontSize = 14,
            Padding = new Thickness(24, 12)
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("IsDownloading"));
        cancelButton.Click += (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
                vm.CancelDownload();
        };
        buttonStack.Children.Add(cancelButton);

        // Skip button
        var skipButton = new Button
        {
            Content = "Skip for Now",
            FontSize = 14,
            Padding = new Thickness(24, 12)
        };
        skipButton.Classes.Add("secondary");
        skipButton.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("HasRequiredPending")
        {
            Converter = Avalonia.Data.Converters.BoolConverters.Not
        });
        skipButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("CanSkip"));
        skipButton.Click += (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
                vm.Skip();
        };
        buttonStack.Children.Add(skipButton);

        container.Children.Add(buttonStack);

        // ==========================================
        // Progress bar (only visible during download)
        // ==========================================
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
            Margin = new Thickness(40, 0)
        };
        progressBar.Bind(ProgressBar.ValueProperty, new Avalonia.Data.Binding("OverallProgress"));
        progressBar.Bind(ProgressBar.IsVisibleProperty, new Avalonia.Data.Binding("IsDownloading"));
        container.Children.Add(progressBar);

        // ==========================================
        // Status line (always visible when there's status)
        // ==========================================
        var statusPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };

        // Status icon (shows state)
        var statusIcon = new TextBlock
        {
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusIcon.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DownloadStatusIcon"));
        statusIcon.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("DownloadStatusColor")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<string, IBrush>(color =>
                new SolidColorBrush(Color.Parse(color ?? "#888888")))
        });
        statusPanel.Children.Add(statusIcon);

        // Status text
        var statusText = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            VerticalAlignment = VerticalAlignment.Center
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DownloadStatus"));
        statusPanel.Children.Add(statusText);

        // Speed text (during download)
        var speedText = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            VerticalAlignment = VerticalAlignment.Center
        };
        speedText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DownloadSpeed"));
        speedText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("IsDownloading"));
        statusPanel.Children.Add(speedText);

        // Separator
        var separator = new TextBlock
        {
            Text = "•",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            VerticalAlignment = VerticalAlignment.Center
        };
        separator.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasDownloadStatus"));
        statusPanel.Children.Add(separator);

        // Open Log link
        var openLogLink = new Button
        {
            Content = "Open Log",
            FontSize = 12,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#6B9FFF")),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };
        openLogLink.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("HasDownloadStatus"));
        openLogLink.Click += (_, _) => ModkitLog.OpenLogFile();
        statusPanel.Children.Add(openLogLink);

        // Hide status panel when no status
        statusPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("HasDownloadStatus"));

        container.Children.Add(statusPanel);

        return container;
    }
}
