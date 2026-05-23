namespace CNC.Core;

public struct UiColor
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; } = 255;

    public UiColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static UiColor Black => new(0, 0, 0);
    public static UiColor LightGreen => new(144, 238, 144);
    public static UiColor Red => new(255, 0, 0);
    public static UiColor Yellow => new(255, 255, 0);
    public static UiColor LightSalmon => new(255, 160, 122);
    public static UiColor Beige => new(245, 245, 220);
    public static UiColor LightSkyBlue => new(135, 206, 250);
    public static UiColor White => new(255, 255, 255);
    public static UiColor LightPink => new(255, 182, 193);
    public static UiColor Green => new(0, 128, 0);
    public static UiColor Gray => new(128, 128, 128);
    public static UiColor Crimson => new(220, 20, 60);
}
