using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.VisualTree;

namespace Predator;

public sealed class TextFilterChangedEventArgs : EventArgs
{
    public TextFilterChangedEventArgs(string text)
    {
        Text = text;
        SelectedValues = Array.Empty<string>();
    }

    public TextFilterChangedEventArgs(IReadOnlyList<string> selectedValues)
    {
        SelectedValues = selectedValues;
        Text = string.Join("; ", selectedValues);
    }

    public string Text { get; }
    public IReadOnlyList<string> SelectedValues { get; }
}

public sealed class TextFilterComboBox : ComboBox
{
    private sealed class FilterOption
    {
        public FilterOption(string value, bool isChecked)
        {
            Value = value;
            IsChecked = isChecked;
        }

        public string Value { get; }
        public bool IsChecked { get; set; }
        public override string ToString() => Value;
    }

    private static readonly Thickness EditableTextPadding = new(7, 5, 0, 0);
    private TextBox? _editableTextBox;
    private bool _suppressTypedTextEvent;
    private bool _suppressSelectionChangedEvent;
    private bool _isInternalItemsSourceUpdate;
    private bool _suppressOptionEvents;
    private readonly List<string> _selectedValues = [];
    private List<FilterOption> _options = [];

    public event EventHandler<TextFilterChangedEventArgs>? TypedTextChanged;
    public event EventHandler<TextFilterChangedEventArgs>? ItemSelected;

    protected override Type StyleKeyOverride => typeof(ComboBox);

    public TextFilterComboBox()
    {
        IsEditable = true;
        ItemTemplate = BuildCheckBoxTemplate();
        SelectionChanged += OnSelectionChanged;
        DropDownClosed += (_, _) => ClearEditableSelection();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _editableTextBox = e.NameScope.Find<TextBox>("PART_EditableTextBox")
            ?? this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        if (_editableTextBox is null)
            return;

        _editableTextBox.Padding = EditableTextPadding;
        _editableTextBox.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        ClearEditableSelection();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty && !_isInternalItemsSourceUpdate)
        {
            RebuildOptionsFromExternalItems(change.GetNewValue<IEnumerable?>());
            return;
        }

        if (change.Property != TextProperty)
            return;

        OnTextChanged(change.GetNewValue<string?>());
    }

    private void OnTextChanged(string? text)
    {
        if (_suppressTypedTextEvent)
            return;

        var currentText = text ?? string.Empty;
        if (SelectedItem is not null)
        {
            var selectedText = SelectedItem.ToString() ?? string.Empty;
            // Ignore the Text update produced by selecting an item in the dropdown.
            if (string.Equals(currentText, selectedText, StringComparison.CurrentCulture))
                return;
        }

        if (SelectedItem is not null)
        {
            _suppressSelectionChangedEvent = true;
            SelectedItem = null;
            _suppressSelectionChangedEvent = false;
        }

        if (_selectedValues.Count > 0)
        {
            _selectedValues.Clear();
            _suppressOptionEvents = true;
            foreach (var option in _options)
                option.IsChecked = false;
            _suppressOptionEvents = false;
        }

        TypedTextChanged?.Invoke(this, new TextFilterChangedEventArgs(currentText));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChangedEvent)
            return;

        if (SelectedItem is null)
        {
            ClearEditableSelection();
            return;
        }

        if (e.AddedItems.Count == 0)
        {
            ClearEditableSelection();
            return;
        }

        // Keep ComboBox selection empty; checked state is handled by checkbox clicks.
        _suppressSelectionChangedEvent = true;
        SelectedItem = null;
        _suppressSelectionChangedEvent = false;

        IsDropDownOpen = true;
        ClearEditableSelection();
    }

    private IDataTemplate BuildCheckBoxTemplate()
    {
        return new FuncDataTemplate<FilterOption>((option, _) =>
        {
            var checkBox = new CheckBox
            {
                Content = option.Value,
                IsChecked = option.IsChecked,
                Margin = new Thickness(2, 0)
            };

            checkBox.PropertyChanged += (_, args) =>
            {
                if (args.Property != ToggleButton.IsCheckedProperty)
                    return;

                var isChecked = args.NewValue is bool value && value;
                OnOptionCheckChanged(option, isChecked);
            };
            return checkBox;
        }, supportsRecycling: true);
    }

    private void RebuildOptionsFromExternalItems(IEnumerable? rawItems)
    {
        var selectedLookup = new HashSet<string>(_selectedValues, StringComparer.CurrentCultureIgnoreCase);

        _options = (rawItems ?? Array.Empty<object>())
            .Cast<object?>()
            .Select(item => item?.ToString()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Select(value => new FilterOption(value, selectedLookup.Contains(value)))
            .ToList();

        _selectedValues.Clear();
        foreach (var option in _options.Where(o => o.IsChecked))
            _selectedValues.Add(option.Value);

        _isInternalItemsSourceUpdate = true;
        ItemsSource = _options;
        _isInternalItemsSourceUpdate = false;

        UpdateTextFromSelectedValues();
    }

    private void OnOptionCheckChanged(FilterOption option, bool isChecked)
    {
        if (_suppressOptionEvents)
            return;

        option.IsChecked = isChecked;

        if (isChecked)
        {
            if (!_selectedValues.Any(v => string.Equals(v, option.Value, StringComparison.CurrentCultureIgnoreCase)))
                _selectedValues.Add(option.Value);
        }
        else
        {
            _selectedValues.RemoveAll(v => string.Equals(v, option.Value, StringComparison.CurrentCultureIgnoreCase));
        }

        UpdateTextFromSelectedValues();
        ItemSelected?.Invoke(this, new TextFilterChangedEventArgs(_selectedValues.ToList()));

        // Keep list open while selecting multiple values.
        Dispatcher.UIThread.Post(() => IsDropDownOpen = true, DispatcherPriority.Background);
    }

    private void UpdateTextFromSelectedValues()
    {
        var combined = _selectedValues.Count switch
        {
            0 => string.Empty,
            1 => _selectedValues[0],
            _ => $"{_selectedValues.Count} valores"
        };

        _suppressTypedTextEvent = true;
        Text = combined;
        _suppressTypedTextEvent = false;
    }

    private void ClearEditableSelection()
    {
        var editableTextBox = _editableTextBox;
        if (editableTextBox is null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_editableTextBox is null)
                return;

            const int caret = 0;

            _editableTextBox.SelectionStart = caret;
            _editableTextBox.SelectionEnd = caret;
            _editableTextBox.CaretIndex = caret;
        }, DispatcherPriority.Background);
    }
}
