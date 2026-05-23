using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Views;

public partial class SignalControl : UserControl
{
    static readonly IBrush LedOn = Brushes.Red;
    static readonly IBrush LedOff = Brushes.LightGray;

    public static readonly StyledProperty<bool> IsSetProperty =
        AvaloniaProperty.Register<SignalControl, bool>(nameof(IsSet));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SignalControl, string>(nameof(Label));

    public SignalControl() => InitializeComponent();

    public new bool IsSet { get => GetValue(IsSetProperty); set => SetValue(IsSetProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    static SignalControl()
    {
        IsSetProperty.Changed.AddClassHandler<SignalControl>((c, e) =>
            c.btnLED.Background = (bool)e.NewValue! ? LedOn : LedOff);
    }
}
