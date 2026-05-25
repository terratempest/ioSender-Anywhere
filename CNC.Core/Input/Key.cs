namespace CNC.Core.Input;

public enum Key
{
    None = 0,
    Left = 23,
    Up = 24,
    Right = 25,
    Down = 26,
    PageUp = 19,
    PageDown = 20,
    Home = 36,
    End = 35,
    // Letter / numpad keys use the legacy values expected by the handler.
    D0 = 34,
    A = 44,
    B = 45,
    C = 46,
    H = 72,
    I = 73,
    J = 74,
    K = 75,
    L = 76,
    M = 77,
    N = 78,
    R = 82,
    S = 83,
    U = 85,
    X = 88,
    Y = 89,
    Z = 90,
    NumPad0 = 98,
    NumPad1 = 99,
    NumPad2 = 100,
    NumPad3 = 101,
    NumPad4 = 102,
    NumPad5 = 103,
    NumPad6 = 104,
    NumPad7 = 105,
    NumPad8 = 106,
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public sealed class KeyEventInfo
{
    public Key Key { get; init; }
    public Key SystemKey { get; init; }
    public ModifierKeys Modifiers { get; init; }
    public bool IsUp { get; init; }
    public bool IsDown => !IsUp;
    public bool IsRepeat { get; init; }
}

public static class KeyInputState
{
    public static ModifierKeys Modifiers { get; set; }
    public static bool IsTextInputFocused { get; set; }
}
