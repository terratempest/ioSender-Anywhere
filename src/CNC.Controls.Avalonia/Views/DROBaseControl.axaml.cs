using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CNC.Controls.Avalonia.Utilities;

namespace CNC.Controls.Avalonia.Views;

public partial class DROBaseControl : UserControl
{
    public const string PlaceholderText = "XXX.XXX";
    static readonly IBrush ScaledBackground = new SolidColorBrush(Color.FromArgb(48, 255, 235, 59));
    readonly NumericProperties _format = new();
    IBrush? _normalPanelBackground;

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DROBaseControl, string>(nameof(Label));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<DROBaseControl, double>(nameof(Value));

    public static readonly StyledProperty<double> MachineValueProperty =
        AvaloniaProperty.Register<DROBaseControl, double>(nameof(MachineValue));

    public static readonly StyledProperty<bool> IsScaledProperty =
        AvaloniaProperty.Register<DROBaseControl, bool>(nameof(IsScaled));

    public DROBaseControl()
    {
        InitializeComponent();
        _normalPanelBackground = readoutPanel.Background;
        btnZero.Click += (_, _) => ZeroClick?.Invoke(this, EventArgs.Empty);
        txtWorkDisplay.Text = PlaceholderText;
        txtMachineDisplay.Text = PlaceholderText;
    }

    internal Controls.NumericTextBox TxtWorkReadout => txtWork;
    internal TextBlock TxtWorkDisplay => txtWorkDisplay;
    internal Button BtnZero => btnZero;

    public event EventHandler? ZeroClick;

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double MachineValue { get => GetValue(MachineValueProperty); set => SetValue(MachineValueProperty, value); }
    public bool IsScaled
    {
        get => GetValue(IsScaledProperty);
        set => SetValue(IsScaledProperty, value);
    }

    public bool IsReadOnly
    {
        get => txtWork.IsReadOnly;
        set => txtWork.IsReadOnly = value;
    }

    public new object? Tag
    {
        get => txtWork.Tag;
        set
        {
            txtWork.Tag = value;
            btnZero.Tag = value;
        }
    }

    static DROBaseControl()
    {
        IsScaledProperty.Changed.AddClassHandler<DROBaseControl>((c, e) => c.ApplyScaledStyle((bool)e.NewValue!));

        ValueProperty.Changed.AddClassHandler<DROBaseControl>((c, e) =>
        {
            if (e.NewValue is double v)
                c.UpdateWorkDisplay(v, c._lastFormat);
        });

        MachineValueProperty.Changed.AddClassHandler<DROBaseControl>((c, e) =>
        {
            if (e.NewValue is double v)
                c.UpdateMachineDisplay(v, c._lastFormat);
        });
    }

    string _lastFormat = NumericProperties.MetricFormat;

    public void SetReadouts(double work, double machine, string format)
    {
        _lastFormat = format;
        txtWork.Format = format;
        UpdateWorkDisplay(work, format);
        UpdateMachineDisplay(machine, format);
    }

    void UpdateWorkDisplay(double value, string format)
    {
        _format.Parse(format);
        txtWorkDisplay.Text = FormatValue(value);
        txtWork.Value = double.IsNaN(value) ? double.NaN : value;
        if (!txtWork.IsVisible)
            txtWork.SetDisplayText(txtWorkDisplay.Text ?? PlaceholderText);
    }

    void UpdateMachineDisplay(double value, string format)
    {
        _format.Parse(format);
        txtMachineDisplay.Text = FormatValue(value);
    }

    string FormatValue(double value) =>
        double.IsNaN(value) || double.IsNegativeInfinity(value)
            ? PlaceholderText
            : Math.Round(value, _format.Precision).ToString(_format.DisplayFormat, CultureInfo.InvariantCulture);

    internal void BeginWorkEdit()
    {
        txtWorkDisplay.IsVisible = false;
        txtWork.IsVisible = true;
        txtWork.IsReadOnly = false;
        txtWork.Value = Value;
        txtWork.SetDisplayText(txtWorkDisplay.Text);
        txtWork.Focus();
    }

    internal void EndWorkEdit(bool restoreValue)
    {
        txtWork.IsReadOnly = true;
        txtWork.IsVisible = false;
        txtWorkDisplay.IsVisible = true;
        if (!restoreValue)
            txtWorkDisplay.Text = FormatValue(txtWork.Value);
    }

    void ApplyScaledStyle(bool scaled) =>
        readoutPanel.Background = scaled
            ? ScaledBackground
            : _normalPanelBackground ?? this.FindResource("IoSenderDroReadoutBrush") as IBrush;
}
