using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.Views;

public partial class SidebarFlyoutHost : UserControl
{
    public SidebarFlyoutHost() => InitializeComponent();

    void OnLoaded(object? sender, RoutedEventArgs e) => RebuildRail(includeJog: IncludeJogFlyout);

    public bool IncludeJogFlyout { get; set; } = true;

    public void ApplyLayoutMode(UiLayoutMode mode)
    {
        IncludeJogFlyout = mode != UiLayoutMode.Expanded;
        JogFlyout.HideFlyout();
        RebuildRail(IncludeJogFlyout);
    }

    void RebuildRail(bool includeJog)
    {
        SidebarItem.ResetFlyoutLayout();
        RailButtons.Children.Clear();

        JogFlyout.HideFlyout();
        GotoFlyout.HideFlyout();
        OutlineFlyout.HideFlyout();
        MachinePositionFlyout.HideFlyout();

        if (includeJog)
            RailButtons.Children.Add(new SidebarItem(JogFlyout));
        RailButtons.Children.Add(new SidebarItem(GotoFlyout));
        RailButtons.Children.Add(new SidebarItem(OutlineFlyout));
        RailButtons.Children.Add(new SidebarItem(MachinePositionFlyout));
    }
}
