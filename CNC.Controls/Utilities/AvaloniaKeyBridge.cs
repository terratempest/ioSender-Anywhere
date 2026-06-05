using Avalonia.Input;
using CNC.Core.Input;
using AvKey = global::Avalonia.Input.Key;
using CoreKey = CNC.Core.Input.Key;
using CoreModifiers = CNC.Core.Input.ModifierKeys;

namespace CNC.Controls.Avalonia.Utilities;

/// <summary>Maps Avalonia key events to <see cref="KeyEventInfo"/>.</summary>
public static class AvaloniaKeyBridge
{
    public static KeyEventInfo ToKeyEventInfo(KeyEventArgs e, bool isUp)
    {
        var key = MapKey(e.Key);
        KeyInputState.Modifiers = MapModifiers(e.KeyModifiers);
        KeyInputState.IsTextInputFocused = e.Source is global::Avalonia.Controls.TextBox;

        return new KeyEventInfo
        {
            Key = key,
            SystemKey = key,
            Modifiers = KeyInputState.Modifiers,
            IsUp = isUp,
            IsRepeat = false
        };
    }

    static CoreModifiers MapModifiers(KeyModifiers modifiers)
    {
        CoreModifiers result = CoreModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            result |= CoreModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Control))
            result |= CoreModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            result |= CoreModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Meta))
            result |= CoreModifiers.Windows;
        return result;
    }

    /// <summary>Legacy key values used by <see cref="CNC.Core.KeypressHandler"/>.</summary>
    static CoreKey MapKey(AvKey key) => key switch
    {
        AvKey.Left => CoreKey.Left,
        AvKey.Up => CoreKey.Up,
        AvKey.Right => CoreKey.Right,
        AvKey.Down => CoreKey.Down,
        AvKey.PageUp => CoreKey.PageUp,
        AvKey.PageDown => CoreKey.PageDown,
        AvKey.Home => CoreKey.Home,
        AvKey.End => CoreKey.End,
        AvKey.A => CoreKey.A,
        AvKey.B => CoreKey.B,
        AvKey.C => CoreKey.C,
        AvKey.H => CoreKey.H,
        AvKey.I => CoreKey.I,
        AvKey.J => CoreKey.J,
        AvKey.K => CoreKey.K,
        AvKey.L => CoreKey.L,
        AvKey.M => CoreKey.M,
        AvKey.N => CoreKey.N,
        AvKey.R => CoreKey.R,
        AvKey.S => CoreKey.S,
        AvKey.U => CoreKey.U,
        AvKey.X => CoreKey.X,
        AvKey.Y => CoreKey.Y,
        AvKey.Z => CoreKey.Z,
        AvKey.D0 => CoreKey.D0,
        AvKey.NumPad0 => CoreKey.NumPad0,
        AvKey.NumPad1 => CoreKey.NumPad1,
        AvKey.NumPad2 => CoreKey.NumPad2,
        AvKey.NumPad3 => CoreKey.NumPad3,
        AvKey.NumPad4 => CoreKey.NumPad4,
        AvKey.NumPad5 => CoreKey.NumPad5,
        AvKey.NumPad6 => CoreKey.NumPad6,
        AvKey.NumPad7 => CoreKey.NumPad7,
        AvKey.NumPad8 => CoreKey.NumPad8,
        // Legacy Key.* values, not virtual-key codes.
        AvKey.Space => (CoreKey)18,
        AvKey.F1 => (CoreKey)90,
        AvKey.F2 => (CoreKey)91,
        AvKey.F3 => (CoreKey)92,
        AvKey.F4 => (CoreKey)93,
        AvKey.F5 => (CoreKey)94,
        AvKey.F6 => (CoreKey)95,
        AvKey.F7 => (CoreKey)96,
        AvKey.F8 => (CoreKey)97,
        AvKey.F9 => (CoreKey)98,
        AvKey.F10 => (CoreKey)99,
        AvKey.F11 => (CoreKey)100,
        AvKey.F12 => (CoreKey)101,
        AvKey.OemMinus => (CoreKey)145,
        AvKey.OemPlus => (CoreKey)146,
        _ => CoreKey.None
    };
}
