using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Views;

public partial class StatusPillControl : UserControl
{
    static readonly IBrush ActiveBackground = Brushes.Red;
    IBrush? _inactiveBackground;

    public static readonly StyledProperty<bool> IsSetProperty =
        AvaloniaProperty.Register<StatusPillControl, bool>(nameof(IsSet));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusPillControl, string>(nameof(Label), string.Empty);

    public StatusPillControl()
    {
        InitializeComponent();
        _inactiveBackground = Pill.Background;
    }

    public new bool IsSet { get => GetValue(IsSetProperty); set => SetValue(IsSetProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    static StatusPillControl()
    {
        IsSetProperty.Changed.AddClassHandler<StatusPillControl>((c, e) =>
            c.Pill.Background = (bool)e.NewValue! ? ActiveBackground : c._inactiveBackground);
    }
}
