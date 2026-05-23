using System.Globalization;

using Avalonia;

using Avalonia.Controls;

using Avalonia.Input;

using CNC.Controls.Avalonia.Utilities;



namespace CNC.Controls.Avalonia.Controls;



public class NumericTextBox : TextBox

{

    readonly NumericProperties _np = new();

    bool _updateText = true;



    public NumericTextBox()

    {

        Height = 24;

        TextAlignment = global::Avalonia.Media.TextAlignment.Right;

        if (Format == NumericProperties.MetricFormat)

            NumericProperties.OnFormatChanged(this, _np, Format);

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

        ValueProperty.Changed.AddClassHandler<NumericTextBox>((b, _) => b.SyncTextFromValue());

        FormatProperty.Changed.AddClassHandler<NumericTextBox>((b, e) =>
        {
            NumericProperties.OnFormatChanged(b, b._np, (string)e.NewValue!);
            if (!b.IsReadOnly || string.IsNullOrEmpty(b.Text))
                b.SyncTextFromValue();
        });

        TextProperty.Changed.AddClassHandler<NumericTextBox>((b, _) => b.SyncValueFromText());

    }



    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!IsReadOnly || string.IsNullOrEmpty(Text))
            SyncTextFromValue();
    }



    internal void SyncTextFromValue()
    {
        if (!_updateText)
            return;

        var raw = GetValue(ValueProperty);
        if (double.IsNaN(raw) || double.IsNegativeInfinity(raw))
        {
            if (IsReadOnly && !string.IsNullOrEmpty(Text))
                return;
            Text = string.Empty;
            return;
        }

        Text = Math.Round(raw, _np.Precision).ToString(_np.DisplayFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Sets visible text without parsing (for placeholders and read-only labels).</summary>
    internal void SetDisplayText(string text)
    {
        _updateText = false;
        Text = text;
        _updateText = true;
    }



    private void SyncValueFromText()
    {
        if (IsReadOnly)
            return;

        var currentText = Text ?? string.Empty;

        if (double.TryParse(currentText == string.Empty ? "NaN" : currentText, _np.Styles, CultureInfo.InvariantCulture, out var val))

        {

            if (!IsReadOnly && IsEnabled)

            {

                _updateText = false;

                Value = val;

                _updateText = true;

            }

        }

        else if (currentText == string.Empty || currentText == ".")

        {

            _updateText = false;

            Value = 0d;

            _updateText = true;

        }

        else if (currentText == "-" || currentText == "-.")

        {

            _updateText = false;

            Value = -0d;

            _updateText = true;

        }

        else

            SyncTextFromValue();

    }



    protected override void OnKeyUp(KeyEventArgs e)

    {

        base.OnKeyUp(e);

        if (e.Key == Key.Delete || e.Key == Key.Back)

        {

            var currentText = Text ?? string.Empty;

            var text = SelectionStart >= 0 && SelectionEnd > SelectionStart

                ? currentText.Remove(SelectionStart, SelectionEnd - SelectionStart)

                : currentText;

            _updateText = false;

            Value = double.Parse(text is "" or "." ? "0" : text is "-" or "-." ? "-0" : text, _np.Styles, CultureInfo.InvariantCulture);

            _updateText = true;

        }

    }



    protected override void OnTextInput(TextInputEventArgs e)

    {

        var currentText = Text ?? string.Empty;

        var text = SelectionStart >= 0 && SelectionEnd > SelectionStart

            ? currentText.Remove(SelectionStart, SelectionEnd - SelectionStart)

            : currentText;

        text = text.Insert(CaretIndex, e.Text ?? string.Empty);

        if (!NumericProperties.IsStringNumeric(text, _np))

        {

            e.Handled = true;

            return;

        }



        _updateText = false;

        Value = double.Parse(text is "" or "." ? "0" : text is "-" or "-." ? "-0" : text, _np.Styles, CultureInfo.InvariantCulture);

        _updateText = true;

        base.OnTextInput(e);

    }

}


