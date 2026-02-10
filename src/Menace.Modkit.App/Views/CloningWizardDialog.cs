using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Multi-step wizard dialog for cloning templates with reference injection.
/// </summary>
public class CloningWizardDialog : Window
{
    private readonly CloningWizardViewModel _viewModel;
    private readonly Panel _contentPanel;
    private readonly TextBlock _stepTitle;
    private readonly TextBlock _stepDescription;
    private readonly TextBlock _errorText;
    private readonly Button _backButton;
    private readonly Button _nextButton;

    // Step 1 controls
    private TextBox? _cloneNameBox;
    private CheckBox? _copyPropertiesCheck;

    // Step 2 controls
    private StackPanel? _referencesPanel;
    private readonly List<CheckBox> _referenceCheckboxes = new();

    // Step 3 controls
    private StackPanel? _assetsPanel;

    // Step 4 controls
    private TextBlock? _previewText;

    public CloningWizardDialog(
        string sourceTemplateType,
        string sourceInstanceName,
        string modpackName,
        ReferenceGraphService referenceGraph,
        SchemaService schemaService,
        string vanillaDataPath)
    {
        _viewModel = new CloningWizardViewModel(
            sourceTemplateType,
            sourceInstanceName,
            modpackName,
            referenceGraph,
            schemaService,
            vanillaDataPath);

        Title = "Clone Template Wizard";
        Width = 600;
        Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = false;

        var mainStack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        // Header
        _stepTitle = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        mainStack.Children.Add(_stepTitle);

        _stepDescription = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            TextWrapping = TextWrapping.Wrap
        };
        mainStack.Children.Add(_stepDescription);

        // Source info
        var sourceInfo = new TextBlock
        {
            Text = $"Source: {sourceTemplateType}/{sourceInstanceName}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            Margin = new Thickness(0, 0, 0, 8)
        };
        mainStack.Children.Add(sourceInfo);

        // Content area
        _contentPanel = new StackPanel
        {
            Spacing = 8,
            MinHeight = 280
        };
        var contentBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = _contentPanel,
            MinHeight = 300
        };
        mainStack.Children.Add(contentBorder);

        // Error text
        _errorText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#FF6B6B")),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        mainStack.Children.Add(_errorText);

        // Button row
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _backButton = new Button
        {
            Content = "Back",
            FontSize = 13,
            IsEnabled = false
        };
        _backButton.Classes.Add("secondary");
        _backButton.Click += (_, _) =>
        {
            _viewModel.GoBack();
            UpdateUI();
        };
        buttonRow.Children.Add(_backButton);

        _nextButton = new Button
        {
            Content = "Next",
            FontSize = 13
        };
        _nextButton.Classes.Add("primary");
        _nextButton.Click += OnNextClick;
        buttonRow.Children.Add(_nextButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            FontSize = 13
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += (_, _) => Close(null);
        buttonRow.Children.Add(cancelButton);

        mainStack.Children.Add(buttonRow);
        Content = mainStack;

        UpdateUI();
    }

    private void OnNextClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.IsReviewStep)
        {
            // Generate and close with result
            var result = _viewModel.GenerateResult();
            Close(result);
        }
        else
        {
            _viewModel.GoNext();
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        _stepTitle.Text = _viewModel.StepTitle;
        _stepDescription.Text = _viewModel.StepDescription;
        _backButton.IsEnabled = _viewModel.CanGoBack;
        _nextButton.Content = _viewModel.NextButtonText;
        _nextButton.IsEnabled = _viewModel.CanGoNext;

        _errorText.Text = _viewModel.ValidationError ?? "";
        _errorText.IsVisible = !string.IsNullOrEmpty(_viewModel.ValidationError);

        _contentPanel.Children.Clear();

        if (_viewModel.IsNameStep)
            BuildNameStep();
        else if (_viewModel.IsReferencesStep)
            BuildReferencesStep();
        else if (_viewModel.IsAssetsStep)
            BuildAssetsStep();
        else if (_viewModel.IsReviewStep)
            BuildReviewStep();
    }

    private void BuildNameStep()
    {
        var stack = new StackPanel { Spacing = 12 };

        // Clone name
        stack.Children.Add(CreateLabel("New Clone Name"));
        _cloneNameBox = new TextBox
        {
            Text = _viewModel.CloneName,
            FontSize = 13,
            Watermark = "e.g., enemy.pirate_captain_elite"
        };
        _cloneNameBox.Classes.Add("input");
        _cloneNameBox.TextChanged += (_, _) =>
        {
            _viewModel.CloneName = _cloneNameBox.Text ?? "";
            _nextButton.IsEnabled = _viewModel.CanGoNext;
            _errorText.Text = _viewModel.ValidationError ?? "";
            _errorText.IsVisible = !string.IsNullOrEmpty(_viewModel.ValidationError);
        };
        stack.Children.Add(_cloneNameBox);

        // Copy properties checkbox
        _copyPropertiesCheck = new CheckBox
        {
            Content = "Copy all property values from source",
            IsChecked = _viewModel.CopyAllProperties,
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _copyPropertiesCheck.Click += (_, _) =>
        {
            _viewModel.CopyAllProperties = _copyPropertiesCheck.IsChecked ?? true;
        };
        stack.Children.Add(_copyPropertiesCheck);

        // Info text
        stack.Children.Add(new TextBlock
        {
            Text = "The clone will be created in the DataTemplateLoader registry and can be patched like any other template.",
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0)
        });

        _contentPanel.Children.Add(stack);
    }

    private void BuildReferencesStep()
    {
        var stack = new StackPanel { Spacing = 8 };

        if (!_viewModel.HasReferences)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No collection references found for this template.",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 12
            });
            stack.Children.Add(new TextBlock
            {
                Text = "The clone will be created but won't automatically appear in any lists.",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
        else
        {
            // Header
            stack.Children.Add(new TextBlock
            {
                Text = "The following templates reference this template in collections:",
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Selection buttons
            var buttonRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var selectAllBtn = new Button { Content = "Select All", FontSize = 11 };
            selectAllBtn.Classes.Add("secondary");
            selectAllBtn.Click += (_, _) =>
            {
                _viewModel.SelectAllReferences();
                UpdateReferenceCheckboxes();
            };
            buttonRow.Children.Add(selectAllBtn);

            var deselectAllBtn = new Button { Content = "Deselect All", FontSize = 11 };
            deselectAllBtn.Classes.Add("secondary");
            deselectAllBtn.Click += (_, _) =>
            {
                _viewModel.DeselectAllReferences();
                UpdateReferenceCheckboxes();
            };
            buttonRow.Children.Add(deselectAllBtn);
            stack.Children.Add(buttonRow);

            // References list
            _referencesPanel = new StackPanel { Spacing = 4 };
            _referenceCheckboxes.Clear();

            foreach (var refSel in _viewModel.References)
            {
                var refRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                var cb = new CheckBox
                {
                    IsChecked = refSel.IsSelected,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var localRef = refSel;
                cb.Click += (_, _) =>
                {
                    localRef.IsSelected = cb.IsChecked ?? false;
                    _viewModel.OnReferenceSelectionChanged();
                };
                _referenceCheckboxes.Add(cb);
                refRow.Children.Add(cb);

                refRow.Children.Add(new TextBlock
                {
                    Text = refSel.ShortDisplayText,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });

                _referencesPanel.Children.Add(refRow);
            }

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 180,
                Content = _referencesPanel
            };
            stack.Children.Add(scrollViewer);

            // Strategy selection
            stack.Children.Add(CreateLabel("Injection Strategy (for selected references):"));

            var strategyPanel = new StackPanel { Spacing = 4 };
            var addAlongsideRadio = new RadioButton
            {
                Content = "Add clone alongside source (both in list)",
                IsChecked = _viewModel.GlobalStrategy == CloneInjectionStrategy.AddAlongside,
                Foreground = Brushes.White,
                FontSize = 12,
                GroupName = "Strategy"
            };
            addAlongsideRadio.Click += (_, _) => _viewModel.GlobalStrategy = CloneInjectionStrategy.AddAlongside;
            strategyPanel.Children.Add(addAlongsideRadio);

            var replaceRadio = new RadioButton
            {
                Content = "Replace source with clone",
                IsChecked = _viewModel.GlobalStrategy == CloneInjectionStrategy.ReplaceSource,
                Foreground = Brushes.White,
                FontSize = 12,
                GroupName = "Strategy"
            };
            replaceRadio.Click += (_, _) => _viewModel.GlobalStrategy = CloneInjectionStrategy.ReplaceSource;
            strategyPanel.Children.Add(replaceRadio);

            stack.Children.Add(strategyPanel);
        }

        _contentPanel.Children.Add(stack);
    }

    private void UpdateReferenceCheckboxes()
    {
        for (int i = 0; i < _referenceCheckboxes.Count && i < _viewModel.References.Count; i++)
        {
            _referenceCheckboxes[i].IsChecked = _viewModel.References[i].IsSelected;
        }
    }

    private void BuildAssetsStep()
    {
        var stack = new StackPanel { Spacing = 8 };

        if (!_viewModel.HasAssetDependencies)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No asset dependencies found for this template.",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 12
            });
            stack.Children.Add(new TextBlock
            {
                Text = "You can skip this step.",
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = "This template references the following assets:",
                Foreground = Brushes.White,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _assetsPanel = new StackPanel { Spacing = 8 };

            foreach (var dep in _viewModel.AssetDependencies)
            {
                var assetRow = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#2D2D30")),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 2, 0, 2)
                };

                var assetStack = new StackPanel { Spacing = 4 };

                // Asset name and category
                var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                headerRow.Children.Add(new TextBlock
                {
                    Text = dep.FieldName,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brushes.White,
                    FontSize = 12
                });
                headerRow.Children.Add(new TextBlock
                {
                    Text = $"({dep.Category})",
                    Foreground = new SolidColorBrush(Color.Parse("#888888")),
                    FontSize = 11
                });
                assetStack.Children.Add(headerRow);

                assetStack.Children.Add(new TextBlock
                {
                    Text = dep.OriginalAsset,
                    Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
                    FontSize = 11
                });

                // Warning if present
                if (!string.IsNullOrEmpty(dep.Warning))
                {
                    assetStack.Children.Add(new TextBlock
                    {
                        Text = $"Note: {dep.Warning}",
                        Foreground = new SolidColorBrush(Color.Parse("#FFD93D")),
                        FontSize = 10,
                        FontStyle = FontStyle.Italic
                    });
                }

                // Strategy selector (simplified for now - just show "Keep Original")
                var localDep = dep;
                var strategyCombo = new ComboBox
                {
                    FontSize = 11,
                    MinWidth = 150,
                    ItemsSource = new[] { "Keep Original", "Clone Asset", "Replace with Custom" },
                    SelectedIndex = (int)dep.Strategy
                };
                strategyCombo.SelectionChanged += (_, _) =>
                {
                    localDep.Strategy = (AssetCloneStrategy)(strategyCombo.SelectedIndex);
                };
                assetStack.Children.Add(strategyCombo);

                assetRow.Child = assetStack;
                _assetsPanel.Children.Add(assetRow);
            }

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 220,
                Content = _assetsPanel
            };
            stack.Children.Add(scrollViewer);
        }

        _contentPanel.Children.Add(stack);
    }

    private void BuildReviewStep()
    {
        var stack = new StackPanel { Spacing = 8 };

        stack.Children.Add(new TextBlock
        {
            Text = "Review the changes that will be made:",
            Foreground = Brushes.White,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _previewText = new TextBlock
        {
            Text = _viewModel.GeneratedPatchesPreview,
            FontFamily = new FontFamily("Consolas, Menlo, monospace"),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 250,
            Content = _previewText
        };
        stack.Children.Add(scrollViewer);

        // Summary
        var summaryPanel = new StackPanel
        {
            Margin = new Thickness(0, 12, 0, 0),
            Spacing = 4
        };
        summaryPanel.Children.Add(new TextBlock
        {
            Text = $"Clone: {_viewModel.CloneName}",
            Foreground = Brushes.White,
            FontSize = 12
        });
        summaryPanel.Children.Add(new TextBlock
        {
            Text = $"References to patch: {_viewModel.SelectedReferenceCount}",
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            FontSize = 11
        });
        stack.Children.Add(summaryPanel);

        _contentPanel.Children.Add(stack);
    }

    private static TextBlock CreateLabel(string text) => new TextBlock
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brushes.White,
        Opacity = 0.8,
        Margin = new Thickness(0, 8, 0, 4)
    };
}
