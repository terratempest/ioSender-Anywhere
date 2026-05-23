using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;

namespace CNC.Controls.Avalonia.Views;

public abstract class FlyoutControlBase : UserControl, ISidebarControl
{
    protected FlyoutControlBase(string menuLabel) => MenuLabel = menuLabel;

    public string MenuLabel { get; }
    public Control FlyoutRoot => this;
    public bool IsFlyoutVisible => IsVisible;

    public void HideFlyout() => IsVisible = false;

    public void OnCloseClick(object? sender, RoutedEventArgs e) => HideFlyout();
}
