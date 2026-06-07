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
    void WireShellTabHeaders()
    {
        TabHome.PointerPressed += (_, _) => OnShellTabHeaderPressed(TabHome);
        TabProbing.PointerPressed += (_, _) => OnShellTabHeaderPressed(TabProbing);
        TabOffsets.PointerPressed += (_, _) => OnShellTabHeaderPressed(TabOffsets);
    }

    void OnShellTabHeaderPressed(TabItem tab)
    {
        if (!_shellReady)
            return;

        NavigateTo(PageFromTab(tab));
    }

    void OnShellTabsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressShellEvents || !_shellReady)
            return;

        if (ShellTabs.SelectedItem is not TabItem selectedTab
            || ReferenceEquals(selectedTab, TabSettings))
            return;

        NavigateTo(PageFromTab(selectedTab), fromTabControl: true);
    }

    void OnGrblSettingsClick(object? sender, RoutedEventArgs e) => NavigateTo(ShellPage.GrblSettings);

    void OnAppSettingsClick(object? sender, RoutedEventArgs e) => NavigateTo(ShellPage.AppSettings);

    void NavigateTo(ShellPage page, bool fromTabControl = false)
    {
        if (!_shellReady && page != ShellPage.Home)
            return;

        using var _ = StartupTrace.Measure($"Navigate {page}");
        DeactivatePage(_activePage);

        _activePage = page;

        JobViewControl.IsVisible = page == ShellPage.Home;
        ProbingPageHost.IsVisible = page == ShellPage.Probing;
        OffsetsPageHost.IsVisible = page == ShellPage.Offsets;
        GrblConfigPageHost.IsVisible = page == ShellPage.GrblSettings;
        AppConfigPageHost.IsVisible = page == ShellPage.AppSettings;

        if (!fromTabControl)
        {
            _suppressShellEvents = true;
            try
            {
                ShellTabs.SelectedItem = page switch
                {
                    ShellPage.Home => TabHome,
                    ShellPage.Probing => TabProbing,
                    ShellPage.Offsets => TabOffsets,
                    _ => TabSettings,
                };
            }
            finally
            {
                _suppressShellEvents = false;
            }
        }

        ActivatePage(page);
        UpdateLayoutMenuEnabled();
    }

    ShellPage PageFromTab(TabItem? tab)
    {
        if (ReferenceEquals(tab, TabProbing))
            return ShellPage.Probing;
        if (ReferenceEquals(tab, TabOffsets))
            return ShellPage.Offsets;
        return ShellPage.Home;
    }

    void ActivatePage(ShellPage page)
    {
        switch (page)
        {
            case ShellPage.Probing:
                EnsureProbingView().Activate(true);
                break;
            case ShellPage.Offsets:
                EnsureOffsetsView().Activate(true);
                break;
            case ShellPage.GrblSettings:
                EnsureGrblConfigView().Activate(true);
                break;
            case ShellPage.AppSettings:
                EnsureAppConfigView();
                break;
        }
    }

    void DeactivatePage(ShellPage page)
    {
        switch (page)
        {
            case ShellPage.Probing:
                _probingView?.Activate(false);
                break;
            case ShellPage.Offsets:
                _offsetsView?.Activate(false);
                break;
            case ShellPage.GrblSettings:
                _grblConfigView?.Activate(false);
                break;
        }
    }

    ProbingView EnsureProbingView()
    {
        if (_probingView is not null)
            return _probingView;

        using var _ = StartupTrace.Measure("Create ProbingView");
        _probingView = new ProbingView(_session.AppConfig.Base)
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        ProbingPageHost.Content = _probingView;
        return _probingView;
    }

    OffsetView EnsureOffsetsView()
    {
        if (_offsetsView is not null)
            return _offsetsView;

        using var _ = StartupTrace.Measure("Create OffsetView");
        _offsetsView = new OffsetView
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        OffsetsPageHost.Content = _offsetsView;
        return _offsetsView;
    }

    GrblConfigView EnsureGrblConfigView()
    {
        if (_grblConfigView is not null)
            return _grblConfigView;

        using var _ = StartupTrace.Measure("Create GrblConfigView");
        _grblConfigView = new GrblConfigView(_session.AppConfig.Base)
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        GrblConfigPageHost.Content = _grblConfigView;
        return _grblConfigView;
    }

    AppConfigView EnsureAppConfigView()
    {
        if (_appConfigView is not null)
            return _appConfigView;

        using var _ = StartupTrace.Measure("Create AppConfigView");
        _appConfigView = new AppConfigView(_session.AppConfig, _session.GameController)
        {
            DataContext = AppHostContext.AppConfig.Base,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        _appConfigView.PreviewViewerRequested += OnPreviewViewerRequested;
        AppConfigPageHost.Content = _appConfigView;
        return _appConfigView;
    }
}
