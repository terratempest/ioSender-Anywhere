using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
using CNC.App;
using CNC.Converters;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.DragKnife;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.Views;
using CNC.Localization.Avalonia;
using ioSender.Navigation;
using ioSender.QuickAccess;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Views;
using ioSender.Workspace;

namespace ioSender;

public partial class MainWindow : Window
{
    void InitializeQuickAccessSidebar()
    {
        _quickAccess = new QuickAccessSidebarController(
            LeftQuickAccess,
            RightQuickAccess,
            QuickAccessBackdrop,
            _viewModel);
        SyncQuickAccessFromConfig();
    }

    void SyncQuickAccessFromConfig()
    {
        var cfg = QuickAccessSidebarService.Config;
        cfg.MigrateLegacyDockOnce();

        _suppressSidebarMenuSync = true;
        try
        {
            MnuSidebarLeft.IsChecked = cfg.ShowLeft;
            MnuSidebarRight.IsChecked = cfg.ShowRight;
        }
        finally
        {
            _suppressSidebarMenuSync = false;
        }

        _quickAccess?.ApplyConfig(cfg);
    }

    void OnSidebarLeftMenuClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DeferApplySidebarMenuFromView();
    }

    void OnSidebarRightMenuClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DeferApplySidebarMenuFromView();
    }

    void DeferApplySidebarMenuFromView() =>
        Dispatcher.UIThread.Post(ApplySidebarMenuFromView, DispatcherPriority.Loaded);

    void OnQuickAccessBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _quickAccess?.ClosePanel();
        e.Handled = true;
    }

    void ApplySidebarMenuFromView()
    {
        if (_suppressSidebarMenuSync)
            return;

        var cfg = QuickAccessSidebarService.Config;
        cfg.ShowLeft = MnuSidebarLeft.IsChecked == true;
        cfg.ShowRight = MnuSidebarRight.IsChecked == true;
        cfg.Enabled = cfg.ShowLeft || cfg.ShowRight;

        if ((cfg.ShowLeft || cfg.ShowRight) && cfg.Tabs.Count == 0)
            QuickAccessSidebarDefaults.EnsureDefaultTabs(cfg);

        QuickAccessSidebarService.Persist();
        _quickAccess?.ApplyConfig(cfg);
    }

    void UpdateLayoutMenuEnabled()
    {
        var homeActive = _activePage == ShellPage.Home;
        MnuLockLayout.IsEnabled = homeActive;
        MnuResetLayout.IsEnabled = homeActive;
        MnuLayouts.IsEnabled = homeActive;
        MnuSaveLayout.IsEnabled = homeActive;
        MnuDeleteLayout.IsEnabled = homeActive && CanDeleteActiveLayout();
    }

    private void OnLockLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        var locked = MnuLockLayout.IsChecked == true;
        JobViewControl.WorkspaceHost.IsEditMode = !locked;
        if (locked)
            WorkspaceLayoutService.Persist();
    }

    private void OnResetLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        JobViewControl.WorkspaceHost.ResetToDefault();
        UpdateLayoutMenuEnabled();
    }

    private void OnPresetClassicClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        ApplyLayout(WorkspaceLayoutDefaults.PresetClassic);
    }

    private void OnPresetTouchClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        ApplyLayout(WorkspaceLayoutDefaults.PresetTouch);
    }

    private void OnPresetXLClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        ApplyLayout(WorkspaceLayoutDefaults.PresetXL);
    }

    void ApplyLayout(string layoutName)
    {
        JobViewControl.WorkspaceHost.ApplyLayout(layoutName);
        SyncQuickAccessFromConfig();
        UpdateLayoutMenuEnabled();
    }

    void RebuildLayoutsMenu()
    {
        MnuLayouts.Items.Clear();
        MnuLayouts.Items.Add(MnuPresetClassic);
        MnuLayouts.Items.Add(MnuPresetTouch);
        MnuLayouts.Items.Add(MnuPresetXL);

        var layouts = WorkspaceLayoutFileService.LoadLayouts();
        if (layouts.Count > 0)
            MnuLayouts.Items.Add(new Separator());

        foreach (var layout in layouts)
        {
            var layoutName = layout.Name;
            var item = new MenuItem { Header = layoutName };
            item.Click += (_, _) =>
            {
                if (_shellReady && _activePage == ShellPage.Home)
                    ApplyLayout(layoutName);
            };
            MnuLayouts.Items.Add(item);
        }
    }

    async void OnSaveLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        var dialog = new LayoutNameDialog();
        var name = await dialog.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var root = JobViewControl.WorkspaceHost.CurrentRoot;
        WorkspaceLayoutFileService.Save(name, root, QuickAccessSidebarService.Config);
        WorkspaceLayoutService.SaveRoot(root, name);
        WorkspaceLayoutService.Persist();
        RebuildLayoutsMenu();
        UpdateLayoutMenuEnabled();
    }

    async void OnDeleteLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home || !CanDeleteActiveLayout())
            return;

        var name = WorkspaceLayoutService.ActiveLayoutName;
        var dialog = new LayoutDeleteDialog(name);
        var confirmed = await dialog.ShowDialog<bool?>(this);
        if (confirmed != true)
            return;

        if (WorkspaceLayoutFileService.Delete(name))
            ApplyLayout(WorkspaceLayoutDefaults.PresetClassic);

        RebuildLayoutsMenu();
        UpdateLayoutMenuEnabled();
    }

    static bool CanDeleteActiveLayout()
    {
        var active = WorkspaceLayoutService.ActiveLayoutName;
        return !string.IsNullOrWhiteSpace(active)
            && !WorkspaceLayoutDefaults.IsBuiltIn(active)
            && WorkspaceLayoutFileService.LoadLayouts()
                .Any(l => l.Name.Equals(active, StringComparison.OrdinalIgnoreCase));
    }
}
