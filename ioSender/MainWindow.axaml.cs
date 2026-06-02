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
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.DragKnife;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
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
    private readonly AppSession _session;
    private readonly MainWindowViewModel _viewModel;
    private readonly ConnectionService _connectionService;
    private readonly MachineConnectionCoordinator _connectionCoordinator;
    private readonly MachineConnectionInitializer _connectionInitializer;
    private readonly ProgramService _programService;
    private CameraWindow? _cameraWindow;
    private Window? _sdCardWindow;
    private Window? _latheWizardsWindow;
    private SDCardView? _sdCardView;
    private LatheWizardsView? _latheWizardsView;
    private bool _shellReady;
    private bool _suppressShellEvents;
    private ShellPage _activePage = ShellPage.Home;
    private ProbingView? _probingView;
    private OffsetView? _offsetsView;
    private GrblConfigView? _grblConfigView;
    private AppConfigView? _appConfigView;
    private bool _restoringPlacement;
    private QuickAccessSidebarController? _quickAccess;
    private bool _suppressSidebarMenuSync;
    private bool _suppressCheckModeMenuSync;
    private WindowState _preFullscreenWindowState = WindowState.Normal;

    public MainWindow()
    {
        using var _ = StartupTrace.Measure("MainWindow constructor");
        InitializeComponent();
        RestoreWindowPlacement();
        UpdateWindowChromeState();
        using (StartupTrace.Measure("MainWindow localization"))
            ApplyLocalization();
        _session = AppHostContext.Session;
        _connectionService = _session.Connection;
        _connectionCoordinator = _session.ConnectionCoordinator;
        _connectionInitializer = _session.ConnectionInitializer;
        _programService = _session.Program;
        _viewModel = _session.MainWindow;
        _viewModel.ConnectionChanged += OnConnectionChanged;
        _viewModel.Grbl.PropertyChanged += OnGrblPropertyChanged;
        DataContext = _viewModel;

        UpdateConnectionUi();
        UpdateProgramFileButtons();
        UpdateCheckModeMenu();
        UpdateLayoutMenuEnabled();
        UpdateFloatingPanelMenuEnabled();
        InitializeQuickAccessSidebar();
        WireShellTabHeaders();
        MnuView.SubmenuOpened += (_, _) =>
        {
            UpdateLayoutMenuEnabled();
            UpdateFloatingPanelMenuEnabled();
        };
        MnuLayouts.SubmenuOpened += (_, _) => RebuildLayoutsMenu();
        Opened += OnMainWindowOpened;
        Closing += OnMainWindowClosing;
        PositionChanged += (_, _) => SaveWindowPlacement();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateWindowChromeState();
        }
    }

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.FullScreen)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    void OnWindowMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void OnWindowMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    void OnWindowFullscreenClick(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    void OnWindowCloseClick(object? sender, RoutedEventArgs e) => Close();

    void ToggleMaximized()
    {
        if (WindowState == WindowState.FullScreen)
            return;

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _preFullscreenWindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
            return;
        }

        _preFullscreenWindowState = WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        SaveWindowPlacement();
        WindowState = WindowState.FullScreen;
    }

    void UpdateWindowChromeState()
    {
        var isFullscreen = WindowState == WindowState.FullScreen;
        var isMaximized = WindowState == WindowState.Maximized;

        BtnWindowMaximize.IsEnabled = !isFullscreen;
        IconWindowMaximize.Data = Geometry.Parse(isMaximized
            ? "M8,5 L19,5 L19,16 L16,16 L16,19 L5,19 L5,8 L8,8 Z M10,7 L10,8 L16,8 L16,14 L17,14 L17,7 Z M7,10 L7,17 L14,17 L14,10 Z"
            : "M5,5 L19,5 L19,19 L5,19 Z M7,7 L7,17 L17,17 L17,7 Z");
        ToolTip.SetTip(BtnWindowMaximize, isMaximized ? "Restore" : "Maximize");

        IconWindowFullscreen.Data = Geometry.Parse(isFullscreen
            ? "M8,4 L10,4 L10,10 L4,10 L4,8 L7,8 L7,5 L8,5 Z M14,4 L16,4 L16,7 L19,7 L19,8 L20,8 L20,10 L14,10 Z M4,14 L10,14 L10,20 L8,20 L8,17 L5,17 L5,16 L4,16 Z M14,14 L20,14 L20,16 L17,16 L17,19 L16,19 L16,20 L14,20 Z"
            : "M4,4 L10,4 L10,6 L7,6 L7,9 L5,9 L5,5 L4,5 Z M14,4 L20,4 L20,10 L18,10 L18,7 L15,7 L15,5 L19,5 L19,4 Z M5,14 L7,14 L7,17 L10,17 L10,19 L4,19 L4,18 L5,18 Z M18,14 L20,14 L20,20 L14,20 L14,18 L17,18 L17,15 L18,15 Z");
        ToolTip.SetTip(BtnWindowFullscreen, isFullscreen ? "Exit fullscreen" : "Fullscreen");
    }

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

    private async void OnMainWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnMainWindowOpened;
        StartupTrace.Mark("MainWindow.OnOpened");
        _shellReady = true;
        JobViewControl.WorkspaceHost.IsEditMode = MnuLockLayout.IsChecked != true;
        JobViewControl.WorkspaceHost.LayoutChanged += (_, _) => UpdateLayoutMenuEnabled();
        SyncQuickAccessFromConfig();
        RegisterGCodeExtensions();
        NavigateTo(ShellPage.Home, fromTabControl: false);
        BringToForeground();
        Dispatcher.UIThread.Post(() =>
        {
            using var _ = StartupTrace.Measure("Deferred workspace open");
            JobViewControl.SetLayoutReady(true);
        }, DispatcherPriority.Background);
        if (AppHostContext.StartupArgs.Length > 0)
            StartupFileHandler.TryLoadFromArgs(AppHostContext.StartupArgs);

        if (!TryStartupReconnect() && ShouldAutoConnectOnStartup())
            await ShowPortDialogAsync();
    }

    bool TryStartupReconnect()
    {
        var config = AppHostContext.AppConfig;
        var portParams = config.Base.PortParams;
        if (SavedPortParams.IsPlaceholder(portParams))
            return false;

        if (!_connectionService.TryConnectFromPortParams(portParams, config.Base.ResetDelay)
            || !_connectionCoordinator.AttachAfterConnect(_viewModel.Grbl))
        {
            DisconnectMachine();
            return false;
        }

        if (!FinishConnectionAfterPortDialog(showErrors: false))
        {
            DisconnectMachine();
            return false;
        }

        _viewModel.NotifyConnectionChanged();
        UpdateConnectionUi();
        UpdateFloatingPanelMenuEnabled();
        return true;
    }

    void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!CanDisconnectOrExit())
        {
            e.Cancel = true;
            ShowBusyMessage();
            return;
        }

        SaveWindowPlacement();
        CloseFloatingPanelWindows();
        DisconnectMachine();
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        SaveWindowPlacement();
    }

    void DisconnectMachine()
    {
        _session.Disconnect();
    }

    static bool ShouldAutoConnectOnStartup()
    {
        foreach (var arg in AppHostContext.StartupArgs)
        {
            if (arg.StartsWith("-port", StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith("--port", StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith("-connect", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    void RegisterGCodeExtensions()
    {
        GCodeConverterRegistry.RegisterDefaults();
        DragKnifeOptions.AutoCompress = AppHostContext.AppConfig.Base.AutoCompress;
        MnuTransform.IsEnabled = true;
        UpdateProgramFileButtons();
    }

    public void HandleIpcMessage(string message) => StartupFileHandler.TryLoadFromIpcMessage(message);

    void RestoreWindowPlacement()
    {
        var config = AppHostContext.AppConfig.Base;
        if (!config.KeepWindowSize)
            return;

        _restoringPlacement = true;
        try
        {
            if (config.WindowWidth == -1 || config.WindowMaximized)
            {
                _preFullscreenWindowState = WindowState.Maximized;
                WindowState = WindowState.Maximized;
                if (config.WindowFullscreen)
                    WindowState = WindowState.FullScreen;
                return;
            }

            var screen = config.WindowLeft >= 0 && config.WindowTop >= 0
                ? Screens.ScreenFromPoint(new PixelPoint(
                    (int)Math.Round(config.WindowLeft),
                    (int)Math.Round(config.WindowTop)))
                : Screens.Primary;
            var workArea = screen?.WorkingArea;
            var maxWidth = workArea?.Width ?? 3840;
            var maxHeight = workArea?.Height ?? 2160;

            Width = Math.Max(Math.Min(config.WindowWidth, maxWidth), MinWidth);
            Height = Math.Max(Math.Min(config.WindowHeight, maxHeight), MinHeight);

            if (workArea is { } area && config.WindowLeft >= 0 && config.WindowTop >= 0)
            {
                var left = Clamp((int)Math.Round(config.WindowLeft), area.X, Math.Max(area.X, area.Right - (int)Math.Round(Width)));
                var top = Clamp((int)Math.Round(config.WindowTop), area.Y, Math.Max(area.Y, area.Bottom - (int)Math.Round(Height)));
                Position = new PixelPoint(left, top);
                WindowStartupLocation = WindowStartupLocation.Manual;
            }

            _preFullscreenWindowState = WindowState.Normal;
            if (config.WindowFullscreen)
                WindowState = WindowState.FullScreen;
        }
        finally
        {
            _restoringPlacement = false;
        }
    }

    void SaveWindowPlacement()
    {
        if (_restoringPlacement)
            return;

        var config = AppHostContext.AppConfig.Base;
        if (!config.KeepWindowSize)
            return;

        if (WindowState == WindowState.FullScreen)
        {
            config.WindowFullscreen = true;
            return;
        }

        config.WindowFullscreen = false;
        config.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState != WindowState.Maximized && WindowState != WindowState.Minimized)
        {
            config.WindowWidth = Bounds.Width > 0 ? Bounds.Width : Width;
            config.WindowHeight = Bounds.Height > 0 ? Bounds.Height : Height;
            config.WindowLeft = Position.X;
            config.WindowTop = Position.Y;
        }
    }

    void BringToForeground()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Topmost = true;
        Dispatcher.UIThread.Post(() =>
        {
            Topmost = false;
            Activate();
            Focus();
        }, DispatcherPriority.Loaded);
    }

    static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);

    private void ApplyLocalization()
    {
        Localize.Apply(MnuFile);
        Localize.Apply(MnuFileOpen);
        Localize.Apply(MnuFileSave);
        Localize.Apply(MnuFileExit);
        Localize.Apply(MnuTransform);
        Localize.Apply(MnuDragKnife);
        Localize.Apply(MnuFileConnect);
        Localize.Apply(MnuView);
        Localize.Apply(MnuLockLayout);
        Localize.Apply(MnuResetLayout);
        Localize.Apply(MnuLayouts);
        Localize.Apply(MnuPresetClassic);
        Localize.Apply(MnuPresetTouch);
        Localize.Apply(MnuPresetXL);
        Localize.Apply(MnuSaveLayout);
        Localize.Apply(MnuDeleteLayout);
        Localize.Apply(MnuViewSdCard);
        Localize.Apply(MnuViewLatheWizards);
        Localize.Apply(MnuViewCamera);
        Localize.Apply(MnuRefreshPorts);
        Localize.Apply(MnuSettings);
        Localize.Apply(MnuGrblSettings);
        Localize.Apply(MnuAppSettings);
        Localize.Apply(MnuCheckMode);
        Localize.Apply(MnuHelp);
        Localize.Apply(MnuHelpWiki);
        Localize.Apply(MnuHelpUsageTips);
        Localize.Apply(MnuHelpBriefTour);
        Localize.Apply(MnuHelpVideoTutorials);
        Localize.Apply(MnuHelpErrorsAndAlarms);
        Localize.Apply(MnuHelpAbout);
        Localize.Apply(TabHome);
        Localize.Apply(TabProbing);
        Localize.Apply(TabOffsets);
    }

    private void OnConnectionChanged()
    {
        UpdateConnectionUi();
        UpdateFloatingPanelMenuEnabled();
    }

    void OnGrblPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.FileName)
            or nameof(GrblViewModel.IsFileLoaded)
            or nameof(GrblViewModel.IsPhysicalFileLoaded)
            or nameof(GrblViewModel.IsJobRunning)
            or nameof(GrblViewModel.IsToolChanging))
        {
            UpdateProgramFileButtons();
            UpdateConnectionUi();
        }

        if (e.PropertyName is nameof(GrblViewModel.IsCheckMode)
            or nameof(GrblViewModel.IsJobRunning)
            or nameof(GrblViewModel.IsToolChanging)
            or nameof(GrblViewModel.IsSleepMode))
            UpdateCheckModeMenu();

        if (e.PropertyName is nameof(GrblViewModel.IsReady)
            or nameof(GrblViewModel.LatheModeEnabled))
            UpdateFloatingPanelMenuEnabled();
    }

    void UpdateCheckModeMenu()
    {
        if (_suppressCheckModeMenuSync)
            return;

        var grbl = _viewModel.Grbl;
        _suppressCheckModeMenuSync = true;
        try
        {
            MnuCheckMode.IsChecked = grbl.IsCheckMode;
            MnuCheckMode.IsEnabled = !grbl.IsJobRunning && !grbl.IsSleepMode;
        }
        finally
        {
            _suppressCheckModeMenuSync = false;
        }
    }

    void OnCheckModeMenuClick(object? sender, RoutedEventArgs e)
    {
        if (_suppressCheckModeMenuSync)
            return;

        var grbl = _viewModel.Grbl;
        if (grbl.IsJobRunning || grbl.IsSleepMode)
            return;

        if (MnuCheckMode.IsChecked == true)
            grbl.ExecuteCommand(GrblConstants.CMD_CHECK);
        else if (grbl.IsCheckMode)
            Grbl.Reset();
    }

    private void UpdateConnectionUi()
    {
        var connected = _connectionService.IsConnected;
        var busy = IsMachineBusy();
        BtnServerStatus.IsEnabled = !busy;
        MnuFileConnect.IsEnabled = !busy;
        MnuFileExit.IsEnabled = !busy;
        BtnServerStatus.Background = new SolidColorBrush(connected ? Color.Parse("#4CAF50") : Color.Parse("#FFB74D"));
        ToolTip.SetTip(BtnServerStatus, connected
            ? string.Format(
                Localize.T("ioSender.mainwindow.str_connected", "Connected: {0}"),
                _connectionService.PortParameters ?? string.Empty)
            : _viewModel.DisconnectedStatusMessage);
    }

    void UpdateProgramFileButtons()
    {
        var grbl = _viewModel.Grbl;
        var canMutate = CanMutateProgram();
        BtnOpenProgram.IsEnabled = canMutate;
        MnuFileOpen.IsEnabled = canMutate;
        MnuFileSave.IsEnabled = canMutate && grbl.IsPhysicalFileLoaded && !grbl.IsSDCardJob;
        MnuTransform.IsEnabled = canMutate && grbl.IsFileLoaded && !grbl.IsSDCardJob;
        BtnReloadProgram.IsEnabled = canMutate && grbl.IsPhysicalFileLoaded;
        BtnCloseProgram.IsEnabled = canMutate && grbl.IsFileLoaded;
    }

    bool IsMachineBusy() => _viewModel.Grbl.IsJobRunning || _viewModel.Grbl.IsToolChanging;

    bool CanMutateProgram() => !IsMachineBusy();

    bool CanDisconnectOrExit() => !IsMachineBusy();

    void ShowBusyMessage() =>
        MessageDialogs.ShowError("Stop the active job or tool change before changing the program, disconnecting, or exiting.", "ioSender");

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
        _appConfigView = new AppConfigView(_session.AppConfig)
        {
            DataContext = AppHostContext.AppConfig.Base,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        AppConfigPageHost.Content = _appConfigView;
        return _appConfigView;
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

    void UpdateFloatingPanelMenuEnabled()
    {
        var initialized = _connectionService.IsConnected && _viewModel.Grbl.IsReady;
        MnuViewSdCard.IsEnabled = initialized && GrblInfo.HasFS;
        MnuViewLatheWizards.IsEnabled = initialized && GrblInfo.LatheModeEnabled;
    }

    private async void OnOpenGCodeClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMutateProgram())
        {
            ShowBusyMessage();
            return;
        }

        if (StorageProvider is not { } storage)
            return;

        var path = await PickGCodeOrConvertedPathAsync(storage);
        if (string.IsNullOrEmpty(path))
            return;

        if (!GCodeConverterRegistry.TryLoad(path, GCodeFileTarget.Current, this))
            _programService.Load(path);
    }

    void OnSaveProgramClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMutateProgram())
        {
            ShowBusyMessage();
            return;
        }

        _programService.Save();
    }

    static async Task<string?> PickGCodeOrConvertedPathAsync(IStorageProvider storage)
    {
        var filter = new List<FilePickerFileType>(GCodeFilePicker.FileTypes);
        var converterPatterns = GCodeConverterRegistry.OpenPatterns
            .GroupBy(p => p.Description)
            .Select(g => new FilePickerFileType(g.Key)
            {
                Patterns = g.Select(p => "*." + p.Extension).ToList()
            });
        filter.AddRange(converterPatterns);

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open file",
            AllowMultiple = false,
            FileTypeFilter = filter
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    void OnDragKnifeTransformClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMutateProgram())
        {
            ShowBusyMessage();
            return;
        }

        new DragKnifeViewModel().Apply(this);
    }

    void OnReloadProgramClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMutateProgram())
        {
            ShowBusyMessage();
            return;
        }

        var filename = _viewModel.Grbl.FileName;
        if (!string.IsNullOrWhiteSpace(filename) && _viewModel.Grbl.IsPhysicalFileLoaded)
            _programService.Load(filename);
    }

    void OnCloseProgramClick(object? sender, RoutedEventArgs e)
    {
        if (!CanMutateProgram())
        {
            ShowBusyMessage();
            return;
        }

        _programService.Close();
    }

    private void OnCameraClick(object? sender, RoutedEventArgs e)
    {
        if (_cameraWindow is { IsVisible: true })
        {
            _cameraWindow.Activate();
            return;
        }

        _cameraWindow = new CameraWindow();
        _cameraWindow.Closed += (_, _) => _cameraWindow = null;
        _cameraWindow.Show(this);
    }

    private void OnSdCardClick(object? sender, RoutedEventArgs e)
    {
        if (_sdCardWindow is { IsVisible: true })
        {
            _sdCardWindow.Activate();
            return;
        }

        _sdCardView = new SDCardView
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };
        _sdCardView.FileSelected += OnSdCardFileSelected;

        _sdCardWindow = CreateFloatingPanelWindow(
            Localize.T("ioSender.mainwindow.tab_sdCard", "SD Card"),
            _sdCardView,
            540,
            540);
        _sdCardWindow.Closed += (_, _) =>
        {
            if (_sdCardView is { } view)
            {
                view.FileSelected -= OnSdCardFileSelected;
                view.Activate(false);
            }

            _sdCardView = null;
            _sdCardWindow = null;
        };
        _sdCardWindow.Show(this);
        _sdCardView.Activate(true);
    }

    private void OnLatheWizardsClick(object? sender, RoutedEventArgs e)
    {
        if (_latheWizardsWindow is { IsVisible: true })
        {
            _latheWizardsWindow.Activate();
            return;
        }

        _latheWizardsView = new LatheWizardsView
        {
            DataContext = _viewModel.Grbl,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        };

        _latheWizardsWindow = CreateFloatingPanelWindow(
            Localize.T("ioSender.mainwindow.tab_latheWizards", "Lathe Wizards"),
            _latheWizardsView,
            900,
            540);
        _latheWizardsWindow.Closed += (_, _) =>
        {
            _latheWizardsView?.Activate(false);
            _latheWizardsView = null;
            _latheWizardsWindow = null;
        };
        _latheWizardsWindow.Show(this);
        _latheWizardsView.Activate(true);
    }

    Window CreateFloatingPanelWindow(string title, Control content, double width, double height) =>
        new()
        {
            Title = title,
            Content = content,
            Width = width,
            Height = height,
            MinWidth = Math.Min(width, 360),
            MinHeight = Math.Min(height, 300),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

    void OnSdCardFileSelected(string filename, bool rewind)
    {
        var separator = filename.IndexOf(':');
        var selectedName = separator >= 0 ? filename[(separator + 1)..] : filename;
        var currentName = _viewModel.Grbl.FileName;
        if (currentName.StartsWith("SDCard:", StringComparison.OrdinalIgnoreCase))
            currentName = currentName["SDCard:".Length..];

        if (!string.Equals(currentName, selectedName, StringComparison.OrdinalIgnoreCase))
            _programService.Close();

        _viewModel.Grbl.FileName = filename;
        _viewModel.Grbl.SDRewind = rewind;
        NavigateTo(ShellPage.Home);
        Activate();
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e) => await ShowPortDialogAsync();

    private async void OnServerStatusClick(object? sender, RoutedEventArgs e)
    {
        if (_connectionService.IsConnected)
            OnDisconnectClick(sender, e);
        else
            await ShowPortDialogAsync();
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        if (!CanDisconnectOrExit())
        {
            ShowBusyMessage();
            return;
        }

        DisconnectMachine();
        _viewModel.NotifyConnectionChanged();
        UpdateConnectionUi();
        UpdateFloatingPanelMenuEnabled();
    }

    void CloseFloatingPanelWindows()
    {
        _sdCardWindow?.Close();
        _latheWizardsWindow?.Close();
    }

    private async Task ShowPortDialogAsync()
    {
        var config = AppHostContext.AppConfig.Base;
        var dialog = new PortDialog(
            AppHostContext.Platform.SerialPortDiscovery,
            _connectionCoordinator,
            MainWindowViewModel.Singleton,
            config.PollInterval,
            config.ResetDelay,
            config.PortParams);

        var connected = await dialog.ShowDialog<bool?>(this);
        if (connected != true)
            return;

        if (!FinishConnectionAfterPortDialog())
        {
            DisconnectMachine();
        }
        else if (!string.IsNullOrEmpty(dialog.ConnectedPortParams))
        {
            config.PortParams = dialog.ConnectedPortParams;
            AppHostContext.AppConfig.Save();
        }

        _viewModel.NotifyConnectionChanged();
        UpdateConnectionUi();
        UpdateFloatingPanelMenuEnabled();
    }

    bool FinishConnectionAfterPortDialog(bool showErrors = true)
    {
        var pollInterval = AppHostContext.AppConfig.Base.PollInterval;
        if (_connectionInitializer.Initialize(_viewModel.Grbl, pollInterval))
            return true;

        if (!showErrors)
            return false;

        var message = string.IsNullOrEmpty(_viewModel.Grbl.Message)
            ? "Failed to initialize the controller."
            : _viewModel.Grbl.Message;
        MessageDialogs.ShowError(message, "ioSender");
        return false;
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

    private void OnRefreshPortsClick(object? sender, RoutedEventArgs e) => _viewModel.RefreshPorts();

    private void OnHelpWikiClick(object? sender, RoutedEventArgs e) =>
        OpenHelpLink("https://github.com/terjeio/ioSender/wiki");

    private void OnHelpUsageTipsClick(object? sender, RoutedEventArgs e) =>
        OpenHelpLink("https://github.com/terjeio/ioSender/wiki/Usage-tips");

    private void OnHelpBriefTourClick(object? sender, RoutedEventArgs e) =>
        OpenHelpLink("https://www.grbl.org/single-post/one-sender-to-rule-them-all");

    private void OnHelpVideoTutorialsClick(object? sender, RoutedEventArgs e) =>
        OpenHelpLink("https://youtube.com/playlist?list=PLnSV6o2cRxM5mQQe4ec5cS2J8jBsEciY3");

    private void OnHelpErrorsAndAlarmsClick(object? sender, RoutedEventArgs e) =>
        new ErrorsAndAlarms(Title ?? "ioSender").Show(this);

    private async void OnHelpAboutClick(object? sender, RoutedEventArgs e)
    {
        var about = new About(Title ?? "ioSender", AppHostContext.AppConfig.Base.PortParams)
        {
            DataContext = _viewModel.Grbl
        };
        await about.ShowDialog(this);
    }

    private static void OpenHelpLink(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
