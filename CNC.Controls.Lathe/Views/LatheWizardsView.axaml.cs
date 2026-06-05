using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CNC.Controls.Avalonia.Utilities;
using CNC.Core;

namespace CNC.Controls.Lathe;

public partial class LatheWizardsView : UserControl
{
    bool _active;

    public LatheWizardsView()
    {
        InitializeComponent();
    }

    public void Activate(bool activate)
    {
        if (_active == activate)
            return;

        _active = activate;
        UpdateConnectionOverlay();

        if (tab.SelectedItem is TabItem selected)
            GetWizard(selected)?.Activate(activate);
        else if (activate && tab.Items.Count > 0 && tab.Items[0] is TabItem first)
            GetWizard(first)?.Activate(true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateConnectionOverlay();
        if (!_active && tab.SelectedItem is TabItem selected)
            GetWizard(selected)?.Activate(true);
    }

    void UpdateConnectionOverlay()
    {
        var connected = Comms.com is { IsOpen: true };
        DisconnectedOverlay.IsVisible = _active && !connected;
    }

    private static ILatheWizardTab? GetWizard(TabItem? tabItem)
    {
        if (tabItem == null)
            return null;

        foreach (var uc in UIUtils.FindLogicalChildren<UserControl>(tabItem))
        {
            if (uc is ILatheWizardTab wizard)
                return wizard;
        }

        return null;
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Equals(e.Source, sender))
            return;

        if (!_active)
            return;

        if (e.AddedItems.Count == 1 && e.AddedItems[0] is TabItem added)
            GetWizard(added)?.Activate(true);

        if (e.RemovedItems.Count == 1 && e.RemovedItems[0] is TabItem removed)
            GetWizard(removed)?.Activate(false);

        e.Handled = true;
    }
}
