namespace CNC.App;

public static class AppThemeKeys
{
    public const string Light = "Light";
    public const string LightHighContrast = "LightHighContrast";
    public const string Dark = "Dark";
    public const string DarkHighContrast = "DarkHighContrast";
    public const string Custom = "Custom";

    public static event EventHandler? ThemeApplied;

    public static string Normalize(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme)
            || theme.Equals("default", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Standard", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            return Dark;

        if (theme.Equals("Black", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Darker", StringComparison.OrdinalIgnoreCase))
            return DarkHighContrast;

        if (theme.Equals("White", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Light (bright)", StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Light (High Contrast)", StringComparison.OrdinalIgnoreCase))
            return LightHighContrast;

        if (theme.Equals(Light, StringComparison.OrdinalIgnoreCase))
            return Light;

        if (theme.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase))
            return LightHighContrast;

        if (theme.Equals(DarkHighContrast, StringComparison.OrdinalIgnoreCase)
            || theme.Equals("Dark (High Contrast)", StringComparison.OrdinalIgnoreCase))
            return DarkHighContrast;

        if (theme.Equals(Custom, StringComparison.OrdinalIgnoreCase))
            return Custom;

        return theme.Trim();
    }

    public static void NotifyThemeApplied() => ThemeApplied?.Invoke(null, EventArgs.Empty);
}
