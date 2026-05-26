using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Utilities;

namespace CNC.Controls.Avalonia.Controls;

public class NumericTextBox : TextBox
{
    readonly NumericProperties _np = new();
    bool _isSyncing;
    bool _isEditing;
    string? _lastValidText;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public NumericTextBox()
    {
        TextChanged += OnTextChanged;
        _np.Parse(Format);
    }

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumericTextBox, double>(nameof(Value), double.NaN);

    public double Value
    {
        get
        {
            var v = GetValue(ValueProperty);
            return double.IsNaN(v) ? 0d : v;
        }
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<NumericTextBox, string>(nameof(Format), NumericProperties.MetricFormat);

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    static NumericTextBox()
    {
        ValueProperty.Changed.AddClassHandler<NumericTextBox>((b, _) =>
        {
            if (!b._isSyncing && !b._isEditing)
                b.SyncTextFromValue();
        });

        FormatProperty.Changed.AddClassHandler<NumericTextBox>((b, e) =>
        {
            NumericProperties.OnFormatChanged(b, b._np, (string)e.NewValue!);
            if (!b._isEditing)
                b.SyncTextFromValue();
        });
    }

    public new void Clear()
    {
        _isSyncing = true;
        try
        {
            SetValue(ValueProperty, double.NaN);
            SetCurrentValue(TextProperty, string.Empty);
            _lastValidText = string.Empty;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (!IsReadOnly)
        {
            _isEditing = true;
            _lastValidText = NumericProperties.IsValidPartialText(Text ?? string.Empty, _np)
                ? Text
                : string.Empty;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (IsReadOnly || _isSyncing)
        {
            base.OnTextInput(e);
            return;
        }

        if (!NumericProperties.IsValidPartialText(GetProposedText(e.Text ?? string.Empty), _np))
        {
            e.Handled = true;
            return;
        }

        base.OnTextInput(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        if (!IsReadOnly && _isEditing)
            CommitText();
        base.OnLostFocus(e);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!IsReadOnly || string.IsNullOrEmpty(Text))
            SyncTextFromValue();
    }

    internal void SyncTextFromValue()
    {
        if (_isSyncing || _isEditing)
            return;

        SetTextFromValue();
    }

    void SetTextFromValue()
    {
        _isSyncing = true;
        try
        {
            var raw = GetValue(ValueProperty);
            if (double.IsNaN(raw) || double.IsNegativeInfinity(raw))
            {
                SetCurrentValue(TextProperty, string.Empty);
                _lastValidText = string.Empty;
                return;
            }

            var text = Math.Round(raw, _np.Precision).ToString(_np.DisplayFormat, CultureInfo.InvariantCulture);
            SetCurrentValue(TextProperty, text);
            _lastValidText = text;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>Sets visible text without parsing (for placeholders and read-only labels).</summary>
    internal void SetDisplayText(string? text)
    {
        _isSyncing = true;
        try
        {
            SetCurrentValue(TextProperty, text ?? string.Empty);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsReadOnly || _isSyncing)
            return;

        var text = Text ?? string.Empty;
        if (!NumericProperties.IsValidPartialText(text, _np))
        {
            _isSyncing = true;
            try
            {
                var restore = _lastValidText ?? string.Empty;
                SetCurrentValue(TextProperty, restore);
                CaretIndex = Math.Min(CaretIndex, restore.Length);
            }
            finally
            {
                _isSyncing = false;
            }
            return;
        }

        _lastValidText = text;
        ApplyTextToValue(text);
    }

    public void CommitText()
    {
        ApplyTextToValue(Text);
        _isEditing = false;
        SetTextFromValue();
    }

    void ApplyTextToValue(string? text)
    {
        if (IsReadOnly || !IsEnabled)
            return;

        text ??= string.Empty;
        _isSyncing = true;
        try
        {
            if (NumericProperties.TryParseCommittedText(text, _np, out var val))
                SetValue(ValueProperty, val);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    string GetProposedText(string input)
    {
        var currentText = Text ?? string.Empty;
        var selectionLength = Math.Abs(SelectionStart - SelectionEnd);
        var caretIndex = CaretIndex;
        if (selectionLength != 0)
        {
            var start = Math.Min(SelectionStart, SelectionEnd);
            currentText = currentText.Remove(start, selectionLength);
            caretIndex = start;
        }

        return currentText.Insert(caretIndex, input);
    }
}
