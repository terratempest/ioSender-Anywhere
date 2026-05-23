namespace CNC.Controls.Avalonia.Config;

public enum JogConfigMode
{
    UI = 0,
    Keypad,
    KeypadAndUI
}

public static class JogDefaults
{
    public static readonly int[] MetricFeedrates = { 5, 100, 500, 1000 };
    public static readonly double[] MetricDistances = { .01d, .1d, 1d, 10d };
    public static readonly int[] ImperialFeedrates = { 5, 10, 50, 100 };
    public static readonly double[] ImperialDistances = { .001d, .01d, .1d, 1d };

    public const JogConfigMode Mode = JogConfigMode.UI;
    public const bool LinkStepJogToUI = true;
    public const bool KeyboardEnable = true;
}
