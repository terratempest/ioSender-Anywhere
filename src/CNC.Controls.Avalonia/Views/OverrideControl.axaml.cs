using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class OverrideControl : UserControl
{
    double _lastValue;

    public delegate void CommandGeneratedHandler(byte[] commands, int len);
    public event CommandGeneratedHandler? CommandGenerated;

    public static readonly StyledProperty<bool> OverrideDisabledProperty =
        AvaloniaProperty.Register<OverrideControl, bool>(nameof(OverrideDisabled));

    public static readonly StyledProperty<int> MinimumProperty =
        AvaloniaProperty.Register<OverrideControl, int>(nameof(Minimum), 10);

    public static readonly StyledProperty<int> MaximumProperty =
        AvaloniaProperty.Register<OverrideControl, int>(nameof(Maximum), 200);

    public static readonly StyledProperty<double[]?> TicksProperty =
        AvaloniaProperty.Register<OverrideControl, double[]?>(nameof(Ticks));

    public static readonly StyledProperty<int> TickFrequencyProperty =
        AvaloniaProperty.Register<OverrideControl, int>(nameof(TickFrequency), 1);

    public static readonly StyledProperty<double> SliderValueProperty =
        AvaloniaProperty.Register<OverrideControl, double>(nameof(SliderValue));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<OverrideControl, double>(nameof(Value));

    public static readonly StyledProperty<GrblEncoderMode> EncoderModeProperty =
        AvaloniaProperty.Register<OverrideControl, GrblEncoderMode>(nameof(EncoderMode), GrblEncoderMode.Unknown);

    public OverrideControl()
    {
        InitializeComponent();
        btnOvReset.Click += (_, _) => CommandGenerated?.Invoke(new[] { ResetCommand }, 1);
        slider.PointerPressed += (_, _) => _lastValue = Math.Round(Value);
        slider.PointerCaptureLost += (_, _) => SendOverrideCommands();
        Loaded += (_, _) => Localize.Apply(lblOverride);
    }

    public byte ResetCommand { get; set; }
    public byte FinePlusCommand { get; set; }
    public byte FineMinusCommand { get; set; }
    public byte CoarsePlusCommand { get; set; }
    public byte CoarseMinusCommand { get; set; }

    public bool OverrideDisabled { get => GetValue(OverrideDisabledProperty); set => SetValue(OverrideDisabledProperty, value); }
    public int Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double[]? Ticks { get => GetValue(TicksProperty); set => SetValue(TicksProperty, value); }
    public int TickFrequency { get => GetValue(TickFrequencyProperty); set => SetValue(TickFrequencyProperty, value); }
    public double SliderValue { get => GetValue(SliderValueProperty); set => SetValue(SliderValueProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public GrblEncoderMode EncoderMode { get => GetValue(EncoderModeProperty); set => SetValue(EncoderModeProperty, value); }

    static OverrideControl()
    {
        SliderValueProperty.Changed.AddClassHandler<OverrideControl>((c, e) =>
            c.txtOverride.Text = Math.Round((double)e.NewValue!).ToString() + "%");
        ValueProperty.Changed.AddClassHandler<OverrideControl>((c, e) =>
            c.SliderValue = Math.Round((double)e.NewValue!));
    }

    void SendOverrideCommands()
    {
        var len = 0;
        var cmd = new byte[30];

        if (FinePlusCommand == 0)
        {
            switch ((int)SliderValue)
            {
                case 25: cmd[len++] = CoarseMinusCommand; break;
                case 50: cmd[len++] = FineMinusCommand; break;
                default: cmd[len++] = ResetCommand; break;
            }
        }
        else
        {
            var coarseDelta = Math.Round(SliderValue) - Value;
            var fineDelta = coarseDelta % 10d;
            var coarseCmd = coarseDelta < 0d ? CoarseMinusCommand : CoarsePlusCommand;
            var fineCmd = fineDelta < 0d ? FineMinusCommand : FinePlusCommand;
            coarseDelta = Math.Abs(coarseDelta - fineDelta);
            fineDelta = Math.Abs(fineDelta);
            while (coarseDelta != 0d) { cmd[len++] = coarseCmd; coarseDelta -= 10d; }
            while (fineDelta != 0d) { cmd[len++] = fineCmd; fineDelta -= 1d; }
        }

        if (len > 0)
            CommandGenerated?.Invoke(cmd, len);
    }
}
