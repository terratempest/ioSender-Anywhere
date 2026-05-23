using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CNC.App;
using CNC.Converters;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.DragKnife;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.Localization.Avalonia;
using ioSender.Navigation;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Views;
using ioSender.Workspace;

namespace ioSender;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ConnectionService _connectionService;
    private readonly MachineConnectionCoordinator _connectionCoordinator;
    private readonly MachineConnectionInitializer _connectionInitializer = new();
    private readonly GrblCommandRouter _commandRouter = new();
    private CameraWindow? _cameraWindow;
    private bool _shellReady;
    private bool _suppressShellEvents;
    private ShellPage _activePage = ShellPage.Home;
    private ProbingView? _probingView;
    private OffsetView? _offsetsView;
    private GrblConfigView? _grblConfigView;
    private AppConfigView? _appConfigView;
    private bool _restoringPlacement;

    public MainWindow()
    {
        using var _ = StartupTrace.Measure("MainWindow constructor");
        InitializeComponent();
        RestoreWindowPlacement();
        using (StartupTrace.Measure("MainWindow localization"))
            ApplyLocalization();
        var platform = AppHostContext.Platform;
        _connectionService = new ConnectionService(platform.SerialPortDiscovery, platform.UiDispatcher);
        _connectionCoordinator = new MachineConnectionCoordinator(_connectionService);
        _viewModel = new MainWindowViewModel(platform, _connectionService);
        _viewModel.ConnectionChanged += OnConnectionChanged;
        _viewModel.Grbl.PropertyChanged += OnGrblPropertyChanged;
        DataContext = _viewModel;

        _commandRouter.Attach(_viewModel.Grbl);
        ControlsPlatformContext.CommandRouter = _commandRouter;

        UpdateConnectionUi();
        UpdateProgramFileButtons();
        UpdateLayoutMenuEnabled();
        WireShellTabHeaders();
        MnuView.SubmenuOpened += (_, _) => UpdateLayoutMenuEnabled();
        MnuLayouts.SubmenuOpened += (_, _) => RebuildLayoutsMenu();
        Opened += OnMainWindowOpened;
        Closing += OnMainWindowClosing;
        PositionChanged += (_, _) => SaveWindowPlacement();
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
        return true;
    }

    void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPlacement();
        DisconnectMachine();
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        SaveWindowPlacement();
    }

    void DisconnectMachine()
    {
        _connectionInitializer.Unregister();
        _connectionCoordinator.Detach(_viewModel.Grbl);
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
                WindowState = WindowState.Maximized;
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
        Localize.Apply(MnuFileExit);
        Localize.Apply(MnuTransform);
        Localize.Apply(MnuDragKnife);
        Localize.Apply(MnuFileConnect);
        Localize.Apply(MnuView);
        Localize.Apply(MnuLockLayout);
        Localize.Apply(MnuResetLayout);
        Localize.Apply(MnuLayouts);
        Localize.Apply(MnuPresetCompact);
        Localize.Apply(MnuPresetExpanded);
        Localize.Apply(MnuSaveLayout);
        Localize.Apply(MnuDeleteLayout);
        Localize.Apply(MnuViewCamera);
        Localize.Apply(MnuRefreshPorts);
        Localize.Apply(MnuSettings);
        Localize.Apply(MnuGrblSettings);
        Localize.Apply(MnuAppSettings);
        Localize.Apply(TabHome);
        Localize.Apply(TabProbing);
        Localize.Apply(TabOffsets);
    }

    private void OnConnectionChanged() => UpdateConnectionUi();

    void OnGrblPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.FileName)
            or nameof(GrblViewModel.IsFileLoaded)
            or nameof(GrblViewModel.IsPhysicalFileLoaded))
            UpdateProgramFileButtons();
    }

    private void UpdateConnectionUi()
    {
        var connected = _connectionService.IsConnected;
        BtnServerStatus.Background = new SolidColorBrush(connected ? Color.Parse("#4CAF50") : Color.Parse("#FFB74D"));
        ToolTip.SetTip(BtnServerStatus, connected
            ? string.Format(
                Localize.T("ioSender.mainwindow.str_connected", "Connected: {0}"),
                _connectionService.PortParameters ?? string.Empty)
            : _viewModel.DisconnectedStatusMessage);
    }

    void UpdateProgramFileButtons()
    {
        BtnReloadProgram.IsEnabled = _viewModel.Grbl.IsPhysicalFileLoaded;
        BtnCloseProgram.IsEnabled = _viewModel.Grbl.IsFileLoaded;
    }

    void OnShellTabsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressShellEvents || !_shellReady)
            return;

        NavigateTo(PageFromTab(ShellTabs.SelectedItem as TabItem), fromTabControl: true);
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

        if (page is ShellPage.Home or ShellPage.Probing or ShellPage.Offsets)
        {
            if (!fromTabControl)
            {
                _suppressShellEvents = true;
                ShellTabs.SelectedItem = page switch
                {
                    ShellPage.Home => TabHome,
                    ShellPage.Probing => TabProbing,
                    ShellPage.Offsets => TabOffsets,
                    _ => TabHome,
                };
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
        _probingView = new ProbingView
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
        _grblConfigView = new GrblConfigView
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
        _appConfigView = new AppConfigView
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

    private async void OnOpenGCodeClick(object? sender, RoutedEventArgs e)
    {
        if (StorageProvider is not { } storage)
            return;

        var path = await PickGCodeOrConvertedPathAsync(storage);
        if (string.IsNullOrEmpty(path))
            return;

        if (!GCodeConverterRegistry.TryLoad(path, GCodeFileTarget.Current, this))
            GCodeFileService.Instance.Load(path);
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
        => new DragKnifeViewModel().Apply(this);

    void OnReloadProgramClick(object? sender, RoutedEventArgs e)
    {
        var filename = _viewModel.Grbl.FileName;
        if (!string.IsNullOrWhiteSpace(filename) && _viewModel.Grbl.IsPhysicalFileLoaded)
            GCodeFileService.Instance.Load(filename);
    }

    void OnCloseProgramClick(object? sender, RoutedEventArgs e) => GCodeFileService.Instance.Close();

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
        DisconnectMachine();
        _viewModel.NotifyConnectionChanged();
        UpdateConnectionUi();
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

    private void OnPresetCompactClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        ApplyLayout(WorkspaceLayoutDefaults.PresetCompact);
    }

    private void OnPresetExpandedClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        ApplyLayout(WorkspaceLayoutDefaults.PresetExpanded);
    }

    void ApplyLayout(string layoutName)
    {
        JobViewControl.WorkspaceHost.ApplyLayout(layoutName);
        UpdateLayoutMenuEnabled();
    }

    void RebuildLayoutsMenu()
    {
        MnuLayouts.Items.Clear();
        MnuLayouts.Items.Add(MnuPresetCompact);
        MnuLayouts.Items.Add(MnuPresetExpanded);

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
        WorkspaceLayoutFileService.Save(name, root);
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
            ApplyLayout(WorkspaceLayoutDefaults.PresetCompact);

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

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
