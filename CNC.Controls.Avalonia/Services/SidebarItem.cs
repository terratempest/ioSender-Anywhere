using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace CNC.Controls.Avalonia.Services;

public sealed class SidebarItem : Button
{
    static double _flyoutTop;
    static ISidebarControl? _lastFlyout;

    readonly ISidebarControl _flyout;

    public static void ResetFlyoutLayout()
    {
        _flyoutTop = 0;
        _lastFlyout = null;
    }

    public SidebarItem(ISidebarControl flyout)
    {
        _flyout = flyout;
        Classes.Add("sidebar-rail");
        Width = 75;
        Height = 25;

        var label = flyout.MenuLabel;
        Content = label.Replace("_", string.Empty);

        flyout.FlyoutRoot.Margin = new Thickness(0, _flyoutTop, 22, 0);
        flyout.FlyoutRoot.HorizontalAlignment = HorizontalAlignment.Right;
        flyout.FlyoutRoot.VerticalAlignment = VerticalAlignment.Top;
        flyout.FlyoutRoot.IsVisible = false;
        _flyoutTop += 75;

        Click += (_, _) => ToggleFlyout();
    }

    void ToggleFlyout()
    {
        if (_lastFlyout != null && _lastFlyout != _flyout && _lastFlyout.IsFlyoutVisible)
            _lastFlyout.HideFlyout();

        if (_flyout.IsFlyoutVisible)
            _flyout.HideFlyout();
        else
            _flyout.FlyoutRoot.IsVisible = true;

        _lastFlyout = _flyout.IsFlyoutVisible ? _flyout : null;
    }
}
