#nullable enable

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Styles;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Effect editor - builds effects that generate C# code.
/// </summary>
public class EffectEditorView : UserControl
{
    private EffectDefinition _effect = new();
    private TextBox? _nameBox;
    private ComboBox? _triggerCombo;
    private StackPanel? _conditionsPanel;
    private StackPanel? _actionsPanel;
    private TextBlock? _codePreview;

    public EffectEditorView()
    {
        // Start with Beagle's Concealment as example
        _effect.Name = "BeaglesConcealment";
        _effect.Description = "Non-silent attacks reduce concealment";
        _effect.Trigger = TriggerEvent.SkillUsed;
        _effect.Conditions.Add(new ConditionNode { Property = ConditionProperty.SkillIsAttack });
        _effect.Conditions.Add(new ConditionNode { Property = ConditionProperty.SkillIsSilent, Negate = true });
        _effect.Actions.Add(new ActionNode
        {
            Action = EffectAction.AddEffect,
            Property = "concealment",
            Modifier = -3,
            Rounds = 1
        });

        Content = BuildUI();
        UpdateCodePreview();
    }

    private Control BuildUI()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Background = ThemeColors.BrushBgWindow
        };

        // Left: Effect builder
        var builderPanel = BuildBuilderPanel();
        root.Children.Add(builderPanel);
        Grid.SetColumn(builderPanel, 0);

        // Right: Code preview
        var previewPanel = BuildPreviewPanel();
        root.Children.Add(previewPanel);
        Grid.SetColumn(previewPanel, 1);

        return root;
    }

    private Control BuildBuilderPanel()
    {
        var scroll = new ScrollViewer
        {
            Padding = new Thickness(16)
        };

        var stack = new StackPanel { Spacing = 16 };

        // Header
        stack.Children.Add(new TextBlock
        {
            Text = "EFFECT BUILDER",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = ThemeColors.BrushTextPrimary
        });

        // Name
        var nameSection = new StackPanel { Spacing = 4 };
        nameSection.Children.Add(CreateLabel("Effect Name"));
        _nameBox = new TextBox { Text = _effect.Name };
        _nameBox.TextChanged += (_, _) =>
        {
            _effect.Name = _nameBox.Text ?? "Effect";
            UpdateCodePreview();
        };
        nameSection.Children.Add(_nameBox);
        stack.Children.Add(nameSection);

        // Trigger Event
        var triggerSection = new StackPanel { Spacing = 4 };
        triggerSection.Children.Add(CreateLabel("When (Trigger Event)"));
        _triggerCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<TriggerEvent>(),
            SelectedItem = _effect.Trigger
        };
        _triggerCombo.SelectionChanged += (_, _) =>
        {
            if (_triggerCombo.SelectedItem is TriggerEvent t)
            {
                _effect.Trigger = t;
                UpdateCodePreview();
            }
        };
        triggerSection.Children.Add(_triggerCombo);
        stack.Children.Add(triggerSection);

        // Conditions
        var condHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        condHeader.Children.Add(CreateLabel("If (Conditions)"));
        var addCondBtn = new Button { Content = "+ Add", FontSize = 11, Padding = new Thickness(8, 2) };
        addCondBtn.Click += (_, _) => AddCondition();
        condHeader.Children.Add(addCondBtn);
        stack.Children.Add(condHeader);

        _conditionsPanel = new StackPanel { Spacing = 8 };
        RefreshConditions();
        stack.Children.Add(_conditionsPanel);

        // Actions
        var actHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actHeader.Children.Add(CreateLabel("Then (Actions)"));
        var addActBtn = new Button { Content = "+ Add", FontSize = 11, Padding = new Thickness(8, 2) };
        addActBtn.Click += (_, _) => AddAction();
        actHeader.Children.Add(addActBtn);
        stack.Children.Add(actHeader);

        _actionsPanel = new StackPanel { Spacing = 8 };
        RefreshActions();
        stack.Children.Add(_actionsPanel);

        scroll.Content = stack;
        return new Border
        {
            Child = scroll,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
    }

    private Control BuildPreviewPanel()
    {
        var stack = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(16)
        };

        // Header
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        header.Children.Add(new TextBlock
        {
            Text = "GENERATED C# CODE",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = ThemeColors.BrushTextPrimary
        });

        var copyBtn = new Button { Content = "Copy", FontSize = 11, Padding = new Thickness(12, 4) };
        copyBtn.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(_effect.GenerateCode());
            }
        };
        header.Children.Add(copyBtn);
        stack.Children.Add(header);

        // Code preview
        _codePreview = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Monaco, monospace"),
            FontSize = 12,
            Foreground = ThemeColors.BrushTextSecondary,
            TextWrapping = TextWrapping.Wrap
        };

        var codeBorder = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Child = new ScrollViewer { Content = _codePreview }
        };

        stack.Children.Add(codeBorder);

        return stack;
    }

    private void RefreshConditions()
    {
        if (_conditionsPanel == null) return;
        _conditionsPanel.Children.Clear();

        for (int i = 0; i < _effect.Conditions.Count; i++)
        {
            var index = i;
            var cond = _effect.Conditions[i];
            var row = BuildConditionRow(cond, index);
            _conditionsPanel.Children.Add(row);
        }

        if (_effect.Conditions.Count == 0)
        {
            _conditionsPanel.Children.Add(new TextBlock
            {
                Text = "(no conditions - always triggers)",
                Foreground = ThemeColors.BrushTextMuted,
                FontStyle = FontStyle.Italic
            });
        }
    }

    private Control BuildConditionRow(ConditionNode cond, int index)
    {
        var row = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        // Negate checkbox
        var negateCheck = new CheckBox
        {
            Content = "NOT",
            IsChecked = cond.Negate,
            FontSize = 11
        };
        negateCheck.IsCheckedChanged += (_, _) =>
        {
            cond.Negate = negateCheck.IsChecked == true;
            UpdateCodePreview();
        };
        stack.Children.Add(negateCheck);

        // Property combo
        var propCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<ConditionProperty>(),
            SelectedItem = cond.Property,
            MinWidth = 150
        };
        propCombo.SelectionChanged += (_, _) =>
        {
            if (propCombo.SelectedItem is ConditionProperty p)
            {
                cond.Property = p;
                UpdateCodePreview();
            }
        };
        stack.Children.Add(propCombo);

        // Value input for certain conditions
        if (cond.Property is ConditionProperty.DamageGreaterThan or ConditionProperty.DamageLessThan
            or ConditionProperty.HealthBelow or ConditionProperty.HealthAbove)
        {
            var valueBox = new TextBox { Text = cond.IntValue.ToString(), Width = 60 };
            valueBox.TextChanged += (_, _) =>
            {
                if (int.TryParse(valueBox.Text, out var v))
                {
                    cond.IntValue = v;
                    UpdateCodePreview();
                }
            };
            stack.Children.Add(valueBox);
        }

        // Remove button
        var removeBtn = new Button { Content = "X", FontSize = 10, Padding = new Thickness(6, 2) };
        removeBtn.Click += (_, _) =>
        {
            _effect.Conditions.RemoveAt(index);
            RefreshConditions();
            UpdateCodePreview();
        };
        stack.Children.Add(removeBtn);

        row.Child = stack;
        return row;
    }

    private void RefreshActions()
    {
        if (_actionsPanel == null) return;
        _actionsPanel.Children.Clear();

        for (int i = 0; i < _effect.Actions.Count; i++)
        {
            var index = i;
            var action = _effect.Actions[i];
            var row = BuildActionRow(action, index);
            _actionsPanel.Children.Add(row);
        }

        if (_effect.Actions.Count == 0)
        {
            _actionsPanel.Children.Add(new TextBlock
            {
                Text = "(no actions)",
                Foreground = ThemeColors.BrushTextMuted,
                FontStyle = FontStyle.Italic
            });
        }
    }

    private Control BuildActionRow(ActionNode action, int index)
    {
        var row = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };

        var stack = new StackPanel { Spacing = 4 };

        // First row: action type
        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var actionCombo = new ComboBox
        {
            ItemsSource = Enum.GetValues<EffectAction>(),
            SelectedItem = action.Action,
            MinWidth = 120
        };
        actionCombo.SelectionChanged += (_, _) =>
        {
            if (actionCombo.SelectedItem is EffectAction a)
            {
                action.Action = a;
                RefreshActions();
                UpdateCodePreview();
            }
        };
        row1.Children.Add(actionCombo);

        // Remove button
        var removeBtn = new Button { Content = "X", FontSize = 10, Padding = new Thickness(6, 2) };
        removeBtn.Click += (_, _) =>
        {
            _effect.Actions.RemoveAt(index);
            RefreshActions();
            UpdateCodePreview();
        };
        row1.Children.Add(removeBtn);

        stack.Children.Add(row1);

        // Second row: action parameters
        if (action.Action == EffectAction.AddEffect)
        {
            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            row2.Children.Add(new TextBlock { Text = "Property:", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            var propBox = new TextBox { Text = action.Property, Width = 100 };
            propBox.TextChanged += (_, _) =>
            {
                action.Property = propBox.Text ?? "";
                UpdateCodePreview();
            };
            row2.Children.Add(propBox);

            row2.Children.Add(new TextBlock { Text = "Modifier:", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            var modBox = new TextBox { Text = action.Modifier.ToString(), Width = 50 };
            modBox.TextChanged += (_, _) =>
            {
                if (int.TryParse(modBox.Text, out var v)) action.Modifier = v;
                UpdateCodePreview();
            };
            row2.Children.Add(modBox);

            row2.Children.Add(new TextBlock { Text = "Rounds:", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            var roundsBox = new TextBox { Text = action.Rounds.ToString(), Width = 40 };
            roundsBox.TextChanged += (_, _) =>
            {
                if (int.TryParse(roundsBox.Text, out var v)) action.Rounds = v;
                UpdateCodePreview();
            };
            row2.Children.Add(roundsBox);

            stack.Children.Add(row2);
        }
        else if (action.Action == EffectAction.ApplyDamage || action.Action == EffectAction.Heal)
        {
            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row2.Children.Add(new TextBlock { Text = "Amount:", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            var amtBox = new TextBox { Text = action.Modifier.ToString(), Width = 60 };
            amtBox.TextChanged += (_, _) =>
            {
                if (int.TryParse(amtBox.Text, out var v)) action.Modifier = v;
                UpdateCodePreview();
            };
            row2.Children.Add(amtBox);
            stack.Children.Add(row2);
        }
        else if (action.Action == EffectAction.Log)
        {
            var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row2.Children.Add(new TextBlock { Text = "Message:", VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });
            var msgBox = new TextBox { Text = action.Message, Width = 200 };
            msgBox.TextChanged += (_, _) =>
            {
                action.Message = msgBox.Text ?? "";
                UpdateCodePreview();
            };
            row2.Children.Add(msgBox);
            stack.Children.Add(row2);
        }

        row.Child = stack;
        return row;
    }

    private void AddCondition()
    {
        _effect.Conditions.Add(new ConditionNode { Property = ConditionProperty.SkillIsAttack });
        RefreshConditions();
        UpdateCodePreview();
    }

    private void AddAction()
    {
        _effect.Actions.Add(new ActionNode { Action = EffectAction.AddEffect });
        RefreshActions();
        UpdateCodePreview();
    }

    private void UpdateCodePreview()
    {
        if (_codePreview != null)
        {
            _codePreview.Text = _effect.GenerateCode();
        }
    }

    private static TextBlock CreateLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = ThemeColors.BrushTextSecondary
    };
}
