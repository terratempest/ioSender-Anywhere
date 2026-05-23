using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Controls;

public enum JogDirectionKind
{
    Plain,
    XPlus,
    XMinus,
    YPlus,
    YMinus,
    ZPlus,
    ZMinus
}

public class JogButton : Button
{
    public static readonly StyledProperty<JogDirectionKind> DirectionProperty =
        AvaloniaProperty.Register<JogButton, JogDirectionKind>(nameof(Direction), JogDirectionKind.Plain);

    const double DirectionRadius = 18;

    public event EventHandler? JogStart;
    public event EventHandler? JogEnd;

    static JogButton()
    {
        DirectionProperty.Changed.AddClassHandler<JogButton>((b, _) => b.ApplyDirectionChrome());
    }

    public JogButton()
    {
        Classes.Add("jog-direction");
        Focusable = false;
        ApplyDirectionChrome();
    }

    public JogDirectionKind Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    void ApplyDirectionChrome()
    {
        CornerRadius = Direction switch
        {
            JogDirectionKind.YPlus => new CornerRadius(DirectionRadius, DirectionRadius, 0, 0),
            JogDirectionKind.YMinus => new CornerRadius(0, 0, DirectionRadius, DirectionRadius),
            JogDirectionKind.XPlus => new CornerRadius(0, DirectionRadius, DirectionRadius, 0),
            JogDirectionKind.XMinus => new CornerRadius(DirectionRadius, 0, 0, DirectionRadius),
            JogDirectionKind.ZPlus => new CornerRadius(DirectionRadius, DirectionRadius, 0, 0),
            JogDirectionKind.ZMinus => new CornerRadius(0, 0, DirectionRadius, DirectionRadius),
            _ => new CornerRadius(3)
        };
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Pointer.Capture(this);
        JogStart?.Invoke(this, e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured == this)
            e.Pointer.Capture(null);
        JogEnd?.Invoke(this, e);
        base.OnPointerReleased(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        JogEnd?.Invoke(this, e);
        base.OnPointerCaptureLost(e);
    }
}
