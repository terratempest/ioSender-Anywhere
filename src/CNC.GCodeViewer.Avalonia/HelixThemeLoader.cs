using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Loads Viewport3DX control theme once (not at app startup).</summary>
internal static class HelixThemeLoader
{
    static bool _applied;

    public static void EnsureApplied()
    {
        if (_applied)
            return;

        var theme = (ResourceDictionary)AvaloniaXamlLoader.Load(
            new Uri("avares://HelixToolkit.Avalonia.SharpDX/Styles/Generic.axaml"));
        if (Application.Current?.Resources is ResourceDictionary appResources)
            appResources.MergedDictionaries.Add(theme);
        _applied = true;
    }
}
