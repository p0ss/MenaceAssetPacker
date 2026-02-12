using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class SaveEditorView : UserControl
{
    private Image? _screenshotImage;
    private TextBlock? _screenshotPlaceholder;

    public SaveEditorView()
    {
        Content = BuildUI();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Load saves when view becomes visible
        if (DataContext is SaveEditorViewModel vm)
            vm.LoadSaveFiles();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Subscribe to SelectedSave changes to update screenshot
        if (DataContext is SaveEditorViewModel vm)
        {
            vm.WhenAnyValue(x => x.SelectedSave)
                .Subscribe(_ => UpdateScreenshot());
        }
    }

    private void UpdateScreenshot()
    {
        if (_screenshotImage == null || _screenshotPlaceholder == null)
            return;

        if (DataContext is not SaveEditorViewModel vm || vm.SelectedSave == null)
        {
            _screenshotImage.Source = null;
            _screenshotImage.IsVisible = false;
            _screenshotPlaceholder.IsVisible = true;
            return;
        }

        var screenshotPath = vm.ScreenshotPath;
        if (screenshotPath != null && System.IO.File.Exists(screenshotPath))
        {
            try
            {
                _screenshotImage.Source = new Bitmap(screenshotPath);
                _screenshotImage.IsVisible = true;
                _screenshotPlaceholder.IsVisible = false;
            }
            catch
            {
                _screenshotImage.Source = null;
                _screenshotImage.IsVisible = false;
                _screenshotPlaceholder.IsVisible = true;
            }
        }
        else
        {
            _screenshotImage.Source = null;
            _screenshotImage.IsVisible = false;
            _screenshotPlaceholder.IsVisible = true;
        }
    }

    private Control BuildUI()
    {
        // Main content - binds visibility to whether we have a valid saves folder
        var mainContent = new ContentControl();
        mainContent.Bind(ContentControl.IsVisibleProperty,
            new Avalonia.Data.Binding("ShowNoSavesWarning")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v)
            });

        var warningOverlay = BuildNoSavesWarning();
        warningOverlay.Bind(Control.IsVisibleProperty,
            new Avalonia.Data.Binding("ShowNoSavesWarning"));

        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,4,*")
        };

        // Left panel: Save list (darker panel)
        var leftWrapper = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = BuildSaveListPanel()
        };
        mainGrid.Children.Add(leftWrapper);
        Grid.SetColumn(leftWrapper, 0);

        // Splitter
        var splitter = new GridSplitter
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            ResizeDirection = GridResizeDirection.Columns
        };
        mainGrid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);

        // Right panel: Details (lighter panel)
        mainGrid.Children.Add(BuildDetailsPanel());
        Grid.SetColumn((Control)mainGrid.Children[2], 2);

        mainContent.Content = mainGrid;

        // Overlay both
        var overlayGrid = new Grid();
        overlayGrid.Children.Add(mainContent);
        overlayGrid.Children.Add(warningOverlay);

        return overlayGrid;
    }

    private Control BuildNoSavesWarning()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Padding = new Thickness(48)
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 24,
            MaxWidth = 600
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Save Files Not Available",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.Children.Add(new TextBlock
        {
            Text = "To view and edit save files, you need to:",
            Foreground = Brushes.White,
            Opacity = 0.9,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });

        var stepsPanel = new StackPanel { Spacing = 16, Margin = new Thickness(0, 16, 0, 0) };
        stepsPanel.Children.Add(CreateStep("1", "Set the game install path in Settings"));
        stepsPanel.Children.Add(CreateStep("2", "Play the game and create at least one save"));
        stepsPanel.Children.Add(CreateStep("3", "Return here to browse and edit your saves"));

        stack.Children.Add(stepsPanel);

        var statusText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 24, 0, 0)
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("StatusMessage"));
        stack.Children.Add(statusText);

        // Refresh button
        var refreshButton = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(24, 12),  // Larger for setup screen
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };
        refreshButton.Classes.Add("primary");
        refreshButton.Click += (_, _) =>
        {
            try
            {
                if (DataContext is SaveEditorViewModel vm)
                    vm.LoadSaveFiles();
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[SaveEditorView] Refresh failed: {ex}");
            }
        };
        stack.Children.Add(refreshButton);

        border.Child = stack;
        return border;
    }

    private Control CreateStep(string number, string text)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var numberBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#004f43")),
            CornerRadius = new CornerRadius(16),
            Width = 32,
            Height = 32
        };

        var numberText = new TextBlock
        {
            Text = number,
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        numberBorder.Child = numberText;

        panel.Children.Add(numberBorder);
        panel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        return panel;
    }

    private Control BuildSaveListPanel()
    {
        var border = new Border();  // No background - parent wrapper has it

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };

        // Row 0: Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Padding = new Thickness(16, 12)
        };
        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        headerStack.Children.Add(new TextBlock
        {
            Text = "Save Files",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var refreshBtn = new Button
        {
            Content = "Refresh",
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        refreshBtn.Classes.Add("secondary");
        refreshBtn.Click += (_, _) =>
        {
            try
            {
                if (DataContext is SaveEditorViewModel vm)
                    vm.LoadSaveFiles();
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[SaveEditorView] Header refresh failed: {ex}");
            }
        };
        headerStack.Children.Add(refreshBtn);

        header.Child = headerStack;
        grid.Children.Add(header);
        Grid.SetRow(header, 0);

        // Row 1: Search box
        var searchBox = new TextBox
        {
            Watermark = "Search saves...",
            Margin = new Thickness(8, 8, 8, 0),
            FontSize = 12
        };
        searchBox.Classes.Add("search");
        searchBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("SearchText")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 1);

        // Row 2: Save list
        var listBox = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8)
        };
        listBox.Bind(ListBox.ItemsSourceProperty,
            new Avalonia.Data.Binding("SaveFiles"));
        listBox.Bind(ListBox.SelectedItemProperty,
            new Avalonia.Data.Binding("SelectedSave")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SaveFileHeader>((save, _) =>
        {
            if (save == null)
                return new Border(); // Return empty for null items

            var itemBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#252525")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 2)
            };

            var stack = new StackPanel { Spacing = 4 };

            // Row 1: Name + Type badge
            var nameRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            var nameText = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };
            // Use DisplayName which falls back to FileName
            nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DisplayName"));
            nameRow.Children.Add(nameText);

            var typeBadge = new Border
            {
                Background = GetTypeBadgeColor(save.SaveStateType),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2)
            };
            var typeLabel = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10
            };
            typeLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TypeLabel"));
            typeBadge.Child = typeLabel;
            nameRow.Children.Add(typeBadge);

            // Modded badge - use binding for visibility
            var moddedBadge = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#8B4513")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2)
            };
            moddedBadge.Child = new TextBlock
            {
                Text = "modded",
                Foreground = Brushes.White,
                FontSize = 10
            };
            moddedBadge.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("IsModded"));
            nameRow.Children.Add(moddedBadge);

            stack.Children.Add(nameRow);

            // Row 2: Planet + PlayTime
            var detailRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            var planetText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                FontSize = 11
            };
            planetText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PlanetName"));
            detailRow.Children.Add(planetText);

            var playTimeText = new TextBlock
            {
                Foreground = Brushes.White,
                Opacity = 0.6,
                FontSize = 11
            };
            playTimeText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PlayTimeFormatted"));
            detailRow.Children.Add(playTimeText);
            stack.Children.Add(detailRow);

            // Row 3: Date
            var dateText = new TextBlock
            {
                Foreground = Brushes.White,
                Opacity = 0.5,
                FontSize = 10
            };
            dateText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SaveTime")
            {
                StringFormat = "yyyy-MM-dd HH:mm"
            });
            stack.Children.Add(dateText);

            // Error indicator - use binding for visibility
            var errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
                FontSize = 10,
                FontStyle = FontStyle.Italic
            };
            errorText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ErrorMessage")
            {
                FallbackValue = "Invalid save"
            });
            errorText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("IsValid")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v)
            });
            stack.Children.Add(errorText);

            itemBorder.Child = stack;
            return itemBorder;
        });

        var scrollViewer = new ScrollViewer { Content = listBox };
        grid.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 2);

        // Row 3: Footer with Open Folder button
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Padding = new Thickness(12, 8)
        };
        var openFolderBtn = new Button
        {
            Content = "Open Folder",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openFolderBtn.Classes.Add("secondary");
        openFolderBtn.Click += (_, _) =>
        {
            try
            {
                if (DataContext is SaveEditorViewModel vm)
                    vm.OpenSaveFolder();
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[SaveEditorView] Open folder failed: {ex}");
            }
        };
        footer.Child = openFolderBtn;
        grid.Children.Add(footer);
        Grid.SetRow(footer, 3);

        border.Child = grid;
        return border;
    }

    private static IBrush GetTypeBadgeColor(SaveStateType type)
    {
        return type switch
        {
            SaveStateType.Auto => new SolidColorBrush(Color.Parse("#004f43")),    // Teal (logo color)
            SaveStateType.Quick => new SolidColorBrush(Color.Parse("#4A3068")),   // Purple
            SaveStateType.Manual => new SolidColorBrush(Color.Parse("#3A5A80")),  // Blue
            SaveStateType.Ironman => new SolidColorBrush(Color.Parse("#410511")), // Maroon
            _ => new SolidColorBrush(Color.Parse("#3E3E3E"))
        };
    }

    private Control BuildDetailsPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            Padding = new Thickness(24)
        };

        var mainStack = new StackPanel { Spacing = 24 };

        // Empty state when no save selected
        var emptyState = new TextBlock
        {
            Text = "Select a save file to view details",
            Foreground = Brushes.White,
            Opacity = 0.6,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        emptyState.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("HasSelectedSave")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v)
            });

        var detailsContent = new StackPanel { Spacing = 24 };
        detailsContent.Bind(StackPanel.IsVisibleProperty,
            new Avalonia.Data.Binding("HasSelectedSave"));

        // Header section with screenshot
        var headerSection = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };

        // Screenshot preview
        var screenshotBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            CornerRadius = new CornerRadius(4),
            Width = 192,
            Height = 108,
            Margin = new Thickness(0, 0, 16, 0),
            ClipToBounds = true
        };

        var screenshotGrid = new Grid();

        _screenshotPlaceholder = new TextBlock
        {
            Text = "No Screenshot",
            Foreground = Brushes.White,
            Opacity = 0.4,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        screenshotGrid.Children.Add(_screenshotPlaceholder);

        _screenshotImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            IsVisible = false
        };
        screenshotGrid.Children.Add(_screenshotImage);

        screenshotBorder.Child = screenshotGrid;
        headerSection.Children.Add(screenshotBorder);
        Grid.SetColumn(screenshotBorder, 0);

        // Header info
        var headerInfo = new StackPanel { Spacing = 8 };

        var saveNameLabel = new TextBlock
        {
            Text = "Save Name",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11
        };
        headerInfo.Children.Add(saveNameLabel);

        var saveNameBox = new TextBox
        {
            FontSize = 14
        };
        saveNameBox.Classes.Add("input");
        saveNameBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("EditableSaveGameName")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
        headerInfo.Children.Add(saveNameBox);

        // Unsaved changes indicator
        var unsavedIndicator = new TextBlock
        {
            Text = "Unsaved changes",
            Foreground = new SolidColorBrush(Color.Parse("#CCAA44")),
            FontSize = 11,
            FontStyle = FontStyle.Italic
        };
        unsavedIndicator.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("HasUnsavedChanges"));
        headerInfo.Children.Add(unsavedIndicator);

        headerSection.Children.Add(headerInfo);
        Grid.SetColumn(headerInfo, 1);

        detailsContent.Children.Add(headerSection);

        // Metadata section with editable fields
        var metadataSection = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16)
        };

        var metadataGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto")
        };

        // Row 0: Planet, Operation
        AddEditableTextField(metadataGrid, 0, 0, "Planet", "EditablePlanetName");
        AddEditableTextField(metadataGrid, 0, 2, "Operation", "EditableOperationName");

        // Row 1: Difficulty, Strategy Config
        AddEditableTextField(metadataGrid, 1, 0, "Difficulty", "EditableDifficulty");
        AddEditableTextField(metadataGrid, 1, 2, "Strategy Config", "EditableStrategyConfigName");

        // Row 2: Completed Missions, Operation Length
        AddEditableIntField(metadataGrid, 2, 0, "Missions", "EditableCompletedMissions");
        AddEditableIntField(metadataGrid, 2, 2, "Op Length", "EditableOperationLength");

        // Row 3: Play Time (seconds), Version (read-only)
        AddEditableDoubleField(metadataGrid, 3, 0, "Play Time (sec)", "EditablePlayTimeSeconds");
        AddReadOnlyField(metadataGrid, 3, 2, "Version", "SelectedSave.Version");

        // Row 4: Save Time, Type dropdown
        AddEditableDateField(metadataGrid, 4, 0, "Save Time", "EditableSaveTime");
        AddSaveTypeDropdown(metadataGrid, 4, 2);

        // Row 5: Play time formatted (read-only display)
        AddReadOnlyField(metadataGrid, 5, 0, "Formatted", "EditablePlayTimeFormatted");

        metadataSection.Child = metadataGrid;
        detailsContent.Children.Add(metadataSection);

        // Mod info section (only shows for modded saves)
        var modInfoSection = BuildModInfoSection();
        detailsContent.Children.Add(modInfoSection);

        // Body data section (parsed save content)
        var bodySection = BuildBodyDataSection();
        detailsContent.Children.Add(bodySection);

        // Action buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var saveButton = new Button
        {
            Content = "Save Changes",
            FontSize = 13
        };
        saveButton.Classes.Add("primary");
        saveButton.Click += (_, _) =>
        {
            try
            {
                if (DataContext is SaveEditorViewModel vm)
                    vm.SaveChanges();
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[SaveEditorView] Save failed: {ex}");
            }
        };
        buttonPanel.Children.Add(saveButton);

        var duplicateButton = new Button
        {
            Content = "Duplicate",
            FontSize = 13
        };
        duplicateButton.Classes.Add("secondary");
        duplicateButton.Click += OnDuplicateClick;
        buttonPanel.Children.Add(duplicateButton);

        var deleteButton = new Button
        {
            Content = "Delete",
            FontSize = 13
        };
        deleteButton.Classes.Add("destructive");
        deleteButton.Click += OnDeleteClick;
        buttonPanel.Children.Add(deleteButton);

        detailsContent.Children.Add(buttonPanel);

        // Status message
        var statusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        statusText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("StatusMessage"));
        detailsContent.Children.Add(statusText);

        mainStack.Children.Add(emptyState);
        mainStack.Children.Add(detailsContent);

        // Wrap in ScrollViewer so all content is accessible
        var scrollViewer = new ScrollViewer
        {
            Content = mainStack,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };

        border.Child = scrollViewer;
        return border;
    }

    private async void OnDuplicateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SaveEditorViewModel vm || vm.SelectedSave == null)
                return;

            var dialog = new Window
            {
                Title = "Duplicate Save",
                Width = 450,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"Duplicate '{vm.SelectedSave.FileName}'",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Enter a name for the new save:",
                Foreground = Brushes.White,
                Opacity = 0.8,
                FontSize = 12
            });

            var nameInput = new TextBox
            {
                Text = vm.SelectedSave.SaveGameName + " (copy)",
                FontSize = 12
            };
            nameInput.Classes.Add("input");
            panel.Children.Add(nameInput);

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                FontSize = 12
            };
            cancelBtn.Classes.Add("secondary");
            cancelBtn.Click += (_, _) => dialog.Close();
            buttonRow.Children.Add(cancelBtn);

            var okBtn = new Button
            {
                Content = "Duplicate",
                FontSize = 12
            };
            okBtn.Classes.Add("primary");
            okBtn.Click += (_, _) =>
            {
                var newName = nameInput.Text?.Trim();
                if (!string.IsNullOrEmpty(newName))
                {
                    vm.DuplicateSaveWithName(newName);
                    dialog.Close();
                }
            };
            buttonRow.Children.Add(okBtn);

            panel.Children.Add(buttonRow);
            dialog.Content = panel;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window parentWindow)
                await dialog.ShowDialog(parentWindow);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Duplicate save failed: {ex.Message}");
        }
    }

    private async void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not SaveEditorViewModel vm || vm.SelectedSave == null)
                return;

            var dialog = new Window
            {
                Title = "Confirm Delete",
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16
            };

            panel.Children.Add(new TextBlock
            {
                Text = $"Delete '{vm.SelectedSave.FileName}'?",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold
            });

            panel.Children.Add(new TextBlock
            {
                Text = "This action cannot be undone.",
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 12
            });

            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                FontSize = 12
            };
            cancelBtn.Classes.Add("secondary");
            cancelBtn.Click += (_, _) => dialog.Close();
            buttonRow.Children.Add(cancelBtn);

            var confirmBtn = new Button
            {
                Content = "Delete",
                FontSize = 12
            };
            confirmBtn.Classes.Add("destructive");
            confirmBtn.Click += (_, _) =>
            {
                vm.DeleteSelectedSave();
                dialog.Close();
            };
            buttonRow.Children.Add(confirmBtn);

            panel.Children.Add(buttonRow);
            dialog.Content = panel;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window parentWindow)
                await dialog.ShowDialog(parentWindow);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Delete save failed: {ex.Message}");
        }
    }

    private static void AddLabel(Grid grid, int row, int col, string label)
    {
        var labelText = new TextBlock
        {
            Text = label + ":",
            Foreground = Brushes.White,
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(0, 4, 12, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(labelText);
        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, col);
    }

    private static void AddReadOnlyField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var valueText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 4, 24, 4),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        valueText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding(binding));
        grid.Children.Add(valueText);
        Grid.SetRow(valueText, row);
        Grid.SetColumn(valueText, col + 1);
    }

    private static void AddEditableTextField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var textBox = new TextBox
        {
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        textBox.Classes.Add("input");
        textBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(textBox);
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, col + 1);
    }

    private static void AddEditableIntField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var numericBox = new NumericUpDown
        {
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = 0,
            Maximum = 9999,
            Increment = 1,
            FormatString = "0"
        };
        numericBox.Bind(NumericUpDown.ValueProperty,
            new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(numericBox);
        Grid.SetRow(numericBox, row);
        Grid.SetColumn(numericBox, col + 1);
    }

    private static void AddEditableDoubleField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var numericBox = new NumericUpDown
        {
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = 0,
            Maximum = 999999999,
            Increment = 60,
            FormatString = "0.0"
        };
        numericBox.Bind(NumericUpDown.ValueProperty,
            new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(numericBox);
        Grid.SetRow(numericBox, row);
        Grid.SetColumn(numericBox, col + 1);
    }

    private static void AddEditableDateField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        // Use a TextBox for DateTime since DateTimePicker is complex
        var textBox = new TextBox
        {
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Watermark = "yyyy-MM-dd HH:mm:ss"
        };
        textBox.Classes.Add("input");
        // Bind with string format for display/edit
        textBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding(binding)
            {
                Mode = Avalonia.Data.BindingMode.TwoWay,
                StringFormat = "yyyy-MM-dd HH:mm:ss"
            });
        grid.Children.Add(textBox);
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, col + 1);
    }

    private static void AddSaveTypeDropdown(Grid grid, int row, int col)
    {
        AddLabel(grid, row, col, "Type");

        var comboBox = new ComboBox
        {
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        comboBox.Classes.Add("input");
        comboBox.Bind(ComboBox.ItemsSourceProperty,
            new Avalonia.Data.Binding("SaveStateTypes"));
        comboBox.Bind(ComboBox.SelectedItemProperty,
            new Avalonia.Data.Binding("EditableSaveStateType") { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(comboBox);
        Grid.SetRow(comboBox, row);
        Grid.SetColumn(comboBox, col + 1);
    }

    private static void AddEditableLargeIntField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var numericBox = new NumericUpDown
        {
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = 0,
            Maximum = 999999999,
            Increment = 100,
            FormatString = "N0"
        };
        numericBox.Bind(NumericUpDown.ValueProperty,
            new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(numericBox);
        Grid.SetRow(numericBox, row);
        Grid.SetColumn(numericBox, col + 1);
    }

    private static void AddEditableSeedField(Grid grid, int row, int col, string label, string binding)
    {
        AddLabel(grid, row, col, label);

        var numericBox = new NumericUpDown
        {
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2),
            FontSize = 12,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            Increment = 1,
            FormatString = "0"
        };
        numericBox.Bind(NumericUpDown.ValueProperty,
            new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(numericBox);
        Grid.SetRow(numericBox, row);
        Grid.SetColumn(numericBox, col + 1);
    }

    private static void AddIronmanDisplay(Grid grid, int row, int col)
    {
        AddLabel(grid, row, col, "Ironman");

        var displayPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 2, 16, 2),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Read-only status display
        var statusText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("EditableIronman")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, string>(v => v ? "Yes" : "No")
            });
        displayPanel.Children.Add(statusText);

        // Warning text when ironman is on - saving will disable it
        var warningText = new TextBlock
        {
            Text = "(will be disabled on save)",
            Foreground = new SolidColorBrush(Color.Parse("#CCAA44")),
            FontSize = 10,
            FontStyle = FontStyle.Italic,
            VerticalAlignment = VerticalAlignment.Center
        };
        warningText.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("EditableIronman"));
        displayPanel.Children.Add(warningText);

        grid.Children.Add(displayPanel);
        Grid.SetRow(displayPanel, row);
        Grid.SetColumn(displayPanel, col + 1);
    }

    private Control BuildModInfoSection()
    {
        var container = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#3D2B1F")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4, 0, 0)
        };

        // Only show when save is modded
        container.Bind(Border.IsVisibleProperty,
            new Avalonia.Data.Binding("IsModded"));

        var stack = new StackPanel { Spacing = 8 };

        // Header
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerRow.Children.Add(new TextBlock
        {
            Text = "Modded Save",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#FFA500")),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(headerRow);

        // Loader version
        var loaderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        loaderRow.Children.Add(new TextBlock
        {
            Text = "Loader:",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11
        });
        var loaderVersion = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11
        };
        loaderVersion.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("ModMeta.LoaderVersion"));
        loaderRow.Children.Add(loaderVersion);
        stack.Children.Add(loaderRow);

        // Game version
        var gameVerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        gameVerRow.Children.Add(new TextBlock
        {
            Text = "Game Version:",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11
        });
        var gameVersion = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11
        };
        gameVersion.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("ModMeta.GameVersion"));
        gameVerRow.Children.Add(gameVersion);
        stack.Children.Add(gameVerRow);

        // Mods list
        stack.Children.Add(new TextBlock
        {
            Text = "Active Mods:",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var modsList = new ItemsControl { Background = Brushes.Transparent };
        modsList.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("ModMeta.Mods"));
        modsList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModInfoEntry>((mod, _) =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 2, 0, 0) };

            panel.Children.Add(new TextBlock
            {
                Text = "•",
                Foreground = new SolidColorBrush(Color.Parse("#FFA500")),
                FontSize = 11
            });

            panel.Children.Add(new TextBlock
            {
                Text = mod.Name,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
            });

            panel.Children.Add(new TextBlock
            {
                Text = $"v{mod.Version}",
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 11
            });

            if (!string.IsNullOrEmpty(mod.Author) && mod.Author != "Unknown")
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"by {mod.Author}",
                    Foreground = Brushes.White,
                    Opacity = 0.5,
                    FontSize = 11
                });
            }

            return panel;
        });
        stack.Children.Add(modsList);

        container.Child = stack;
        return container;
    }

    private Control BuildBodyDataSection()
    {
        var outerContainer = new StackPanel { Spacing = 12 };

        // Section header - always show
        outerContainer.Children.Add(new TextBlock
        {
            Text = "Save Data",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 8, 0, 0)
        });


        // Error message when body data fails to parse
        var bodyErrorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),  // Teal text
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        bodyErrorText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("BodyData.ErrorMessage"));
        bodyErrorText.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("HasBodyData")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v)
            });
        outerContainer.Children.Add(bodyErrorText);

        var container = new StackPanel { Spacing = 12 };

        // Only show content when body data is available
        container.Bind(StackPanel.IsVisibleProperty,
            new Avalonia.Data.Binding("HasBodyData"));

        // Resources section header
        container.Children.Add(new TextBlock
        {
            Text = "Resources",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Resources section (editable)
        var resourcesSection = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var resourcesStack = new StackPanel { Spacing = 12 };

        // Resources grid
        var resourcesGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto")
        };

        // Row 0: Credits, Intel
        AddEditableLargeIntField(resourcesGrid, 0, 0, "Credits", "EditableCredits");
        AddEditableLargeIntField(resourcesGrid, 0, 2, "Intel", "EditableIntelligence");

        // Row 1: Authority, Promotion Points
        AddEditableLargeIntField(resourcesGrid, 1, 0, "Authority", "EditableAuthority");
        AddEditableLargeIntField(resourcesGrid, 1, 2, "Promo Pts", "EditablePromotionPoints");

        resourcesStack.Children.Add(resourcesGrid);

        // Ironman/Seed row in a separate panel
        var stateGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*")
        };
        AddIronmanDisplay(stateGrid, 0, 0);
        AddEditableSeedField(stateGrid, 0, 2, "Seed", "EditableSeed");
        resourcesStack.Children.Add(stateGrid);

        resourcesSection.Child = resourcesStack;
        container.Children.Add(resourcesSection);

        // Planets section
        var planetsExpander = CreateExpanderSection("Planets", BuildPlanetsContent());
        container.Children.Add(planetsExpander);

        // Leaders section
        var leadersExpander = CreateExpanderSection("Leaders", BuildLeadersContent());
        container.Children.Add(leadersExpander);

        // Squaddies section
        var squaddiesExpander = CreateExpanderSection("Squaddies", BuildSquaddiesContent());
        container.Children.Add(squaddiesExpander);

        outerContainer.Children.Add(container);
        return outerContainer;
    }

    private Control CreateExpanderSection(string title, Control content)
    {
        var expander = new Expander
        {
            Header = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            },
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Foreground = Brushes.White,
            Padding = new Thickness(8),
            IsExpanded = false
        };
        expander.Content = content;
        return expander;
    }

    private Control BuildPlanetsContent()
    {
        var listBox = new ItemsControl
        {
            Background = Brushes.Transparent
        };
        listBox.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("BodyData.Planets"));

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.PlanetData>((planet, _) =>
        {
            if (planet == null)
                return new Border();

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4) };

            // Planet name
            var nameText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                FontSize = 11,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TemplateName"));
            panel.Children.Add(nameText);

            // Control label
            panel.Children.Add(new TextBlock
            {
                Text = "Control:",
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Editable Control value
            var controlBox = new NumericUpDown
            {
                Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                FontSize = 11,
                Width = 70,
                Minimum = 0,
                Maximum = 100,
                Increment = 5,
                FormatString = "0",
                VerticalAlignment = VerticalAlignment.Center
            };
            controlBox.Bind(NumericUpDown.ValueProperty,
                new Avalonia.Data.Binding("Control") { Mode = Avalonia.Data.BindingMode.TwoWay });
            panel.Children.Add(controlBox);

            panel.Children.Add(new TextBlock
            {
                Text = "%",
                Foreground = Brushes.White,
                Opacity = 0.5,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Change label
            panel.Children.Add(new TextBlock
            {
                Text = "Δ:",
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });

            // Editable ControlChange value
            var changeBox = new NumericUpDown
            {
                Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2),
                FontSize = 11,
                Width = 60,
                Minimum = -100,
                Maximum = 100,
                Increment = 1,
                FormatString = "+0;-0;0",
                VerticalAlignment = VerticalAlignment.Center
            };
            changeBox.Bind(NumericUpDown.ValueProperty,
                new Avalonia.Data.Binding("ControlChange") { Mode = Avalonia.Data.BindingMode.TwoWay });
            panel.Children.Add(changeBox);

            return panel;
        });

        return listBox;
    }

    private Control BuildLeadersContent()
    {
        var stack = new StackPanel { Spacing = 8 };

        // Hired leaders
        stack.Children.Add(new TextBlock
        {
            Text = "Hired",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        });

        var hiredList = new ItemsControl { Background = Brushes.Transparent };
        hiredList.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("BodyData.HiredLeaders"));
        hiredList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.LeaderData>((leader, _) =>
            new TextBlock
            {
                Text = $"  {leader.TemplateName} ({(leader.ActorType == 0 ? "Squad" : "Pilot")})",
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 1)
            });
        stack.Children.Add(hiredList);

        // Dead leaders
        stack.Children.Add(new TextBlock
        {
            Text = "Fallen",
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var deadList = new ItemsControl { Background = Brushes.Transparent };
        deadList.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("BodyData.DeadLeaders"));
        deadList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.LeaderData>((leader, _) =>
            new TextBlock
            {
                Text = $"  {leader.TemplateName}",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 11,
                FontStyle = FontStyle.Italic,
                Margin = new Thickness(0, 1)
            });
        stack.Children.Add(deadList);

        return stack;
    }

    private Control BuildSquaddiesContent()
    {
        var listBox = new ItemsControl { Background = Brushes.Transparent };
        listBox.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("BodyData.Squaddies"));

        listBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.SquaddieData>((squaddie, _) =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2) };

            panel.Children.Add(new TextBlock
            {
                Text = squaddie.FullName,
                Foreground = Brushes.White,
                FontSize = 11,
                Width = 140
            });

            panel.Children.Add(new TextBlock
            {
                Text = squaddie.TemplateName,
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                FontSize = 11
            });

            return panel;
        });

        return listBox;
    }
}
