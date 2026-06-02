using Avalonia.Controls;

namespace CNC.Controls.Avalonia.Controls;

public static class PopupKeyboardTarget
{
    static WeakReference<TextBox>? _target;

    public static TextBox? Current
    {
        get => _target != null && _target.TryGetTarget(out var textBox) ? textBox : null;
        set => _target = value is null ? null : new WeakReference<TextBox>(value);
    }
}
