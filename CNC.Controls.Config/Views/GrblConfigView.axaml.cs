using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Core;
using CNC.Controls.Avalonia.Utilities;

namespace CNC.Controls.Config;

public partial class GrblConfigView : UserControl
{
    bool _activated;
    public GrblConfigView() : this(null)
    {
    }

    public GrblConfigView(BaseConfig? appBase)
    {
        InitializeComponent();
        Configure(appBase);
        SyncOptionalTabs();
    }

    public void Configure(BaseConfig? appBase)
    {
        var pollInterval = appBase?.PollInterval ?? 200;
        trinamicControl.PollInterval = pollInterval;
        stepperCalibration.PollInterval = pollInterval;
    }

    public void Activate(bool activate)
    {
        if (activate)
            SyncOptionalTabs();

        if (activate == _activated)
            return;

        _activated = activate;
        GetView(tabConfig.SelectedItem as TabItem ?? tabConfig.Items[0] as TabItem)?.Activate(activate);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SyncOptionalTabs();
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

    void SyncOptionalTabs()
    {
        var selectedTab = tabConfig.SelectedItem as TabItem;

        PlaceTab(tabTrinamic, !string.IsNullOrEmpty(GrblInfo.TrinamicDrivers), 1);
        PlaceTab(tabPIDTuner, GrblInfo.HasPIDLog, IndexOfTab(tabTrinamic) >= 0 ? 2 : 1);

        if (selectedTab != null && IndexOfTab(selectedTab) < 0)
            tabConfig.SelectedItem = tabConfig.Items.Count > 0 ? tabConfig.Items[0] : null;
    }

    void PlaceTab(TabItem tab, bool show, int targetIndex)
    {
        var currentIndex = IndexOfTab(tab);
        if (!show)
        {
            if (currentIndex >= 0)
                tabConfig.Items.Remove(tab);
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, tabConfig.Items.Count);
        if (currentIndex < 0)
            tabConfig.Items.Insert(targetIndex, tab);
        else if (currentIndex != targetIndex)
        {
            tabConfig.Items.Remove(tab);
            tabConfig.Items.Insert(Math.Min(targetIndex, tabConfig.Items.Count), tab);
        }
    }

    int IndexOfTab(TabItem tab)
    {
        for (var i = 0; i < tabConfig.Items.Count; i++)
        {
            if (ReferenceEquals(tabConfig.Items[i], tab))
                return i;
        }

        return -1;
    }
}
