using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Controls.Avalonia.Utilities;

namespace CNC.Controls.Config;

public partial class GrblConfigView : UserControl
{
    bool _activated;

    public GrblConfigView() => InitializeComponent();

    public void Activate(bool activate)
    {
        if (activate == _activated)
            return;

        _activated = activate;
        GetView(tabConfig.SelectedItem as TabItem ?? tabConfig.Items[0] as TabItem)?.Activate(activate);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (string.IsNullOrEmpty(GrblInfo.TrinamicDrivers))
            RemoveTab(GrblConfigType.Trinamic);
        if (!GrblInfo.HasPIDLog)
            RemoveTab(GrblConfigType.PidTuning);
    }

    void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!Equals(e.Source, sender))
            return;

        if (e.AddedItems.Count == 1)
        {
            if (e.RemovedItems.Count == 1)
                GetView(e.RemovedItems[0] as TabItem)?.Activate(false);
            GetView(e.AddedItems[0] as TabItem)?.Activate(_activated);
        }

        e.Handled = true;
    }

    static IGrblConfigTab? GetView(TabItem? tab)
    {
        if (tab == null)
            return null;

        foreach (var uc in UIUtils.FindLogicalChildren<UserControl>(tab))
        {
            if (uc is IGrblConfigTab configTab)
                return configTab;
        }

        return null;
    }

    void RemoveTab(GrblConfigType type)
    {
        foreach (var tab in UIUtils.FindLogicalChildren<TabItem>(tabConfig))
        {
            if (GetView(tab)?.GrblConfigType == type)
            {
                tabConfig.Items.Remove(tab);
                break;
            }
        }
    }
}
