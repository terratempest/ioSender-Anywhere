using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
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

    public MainWindow()
    {
        InitializeComponent();
        AppConfigViewControl.DataContext = AppHostContext.AppConfig.Base;
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
        Opened += OnMainWindowOpened;
        Closing += OnMainWindowClosing;
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
        _shellReady = true;
        JobViewControl.SetLayoutReady(true);
        JobViewControl.WorkspaceHost.IsEditMode = MnuCustomizeLayout.IsChecked == true;
        RegisterGCodeExtensions();
        NavigateTo(ShellPage.Home, fromTabControl: false);
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

    void OnMainWindowClosing(object? sender, WindowClosingEventArgs e) => DisconnectMachine();

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

    private void ApplyLocalization()
    {
        Localize.Apply(MnuFile);
        Localize.Apply(MnuFileOpen);
        Localize.Apply(MnuFileExit);
        Localize.Apply(MnuTransform);
        Localize.Apply(MnuDragKnife);
        Localize.Apply(MnuFileConnect);
        Localize.Apply(MnuView);
        Localize.Apply(MnuCustomizeLayout);
        Localize.Apply(MnuResetLayout);
        Localize.Apply(MnuLayoutPreset);
        Localize.Apply(MnuPresetCompact);
        Localize.Apply(MnuPresetExpanded);
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

        DeactivatePage(_activePage);

        _activePage = page;

        JobViewControl.IsVisible = page == ShellPage.Home;
        ProbingViewControl.IsVisible = page == ShellPage.Probing;
        OffsetsViewControl.IsVisible = page == ShellPage.Offsets;
        GrblConfigViewControl.IsVisible = page == ShellPage.GrblSettings;
        AppConfigViewControl.IsVisible = page == ShellPage.AppSettings;

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
                ProbingViewControl.Activate(true);
                break;
            case ShellPage.Offsets:
                OffsetsViewControl.Activate(true);
                break;
            case ShellPage.GrblSettings:
                GrblConfigViewControl.Activate(true);
                break;
        }
    }

    void DeactivatePage(ShellPage page)
    {
        switch (page)
        {
            case ShellPage.Probing:
                ProbingViewControl.Activate(false);
                break;
            case ShellPage.Offsets:
                OffsetsViewControl.Activate(false);
                break;
            case ShellPage.GrblSettings:
                GrblConfigViewControl.Activate(false);
                break;
        }
    }

    void UpdateLayoutMenuEnabled()
    {
        var homeActive = _activePage == ShellPage.Home;
        MnuCustomizeLayout.IsEnabled = homeActive;
        MnuResetLayout.IsEnabled = homeActive;
        MnuLayoutPreset.IsEnabled = homeActive;
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

    private void OnCustomizeLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        var edit = MnuCustomizeLayout.IsChecked != true;
        MnuCustomizeLayout.IsChecked = edit;
        JobViewControl.WorkspaceHost.IsEditMode = edit;
        if (!edit)
            WorkspaceLayoutService.Persist();
    }

    private void OnResetLayoutClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        JobViewControl.WorkspaceHost.ResetToDefault();
    }

    private void OnPresetCompactClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        JobViewControl.WorkspaceHost.ApplyPreset(WorkspaceLayoutDefaults.PresetCompact);
    }

    private void OnPresetExpandedClick(object? sender, RoutedEventArgs e)
    {
        if (!_shellReady || _activePage != ShellPage.Home)
            return;

        JobViewControl.WorkspaceHost.ApplyPreset(WorkspaceLayoutDefaults.PresetExpanded);
    }

    private void OnRefreshPortsClick(object? sender, RoutedEventArgs e) => _viewModel.RefreshPorts();

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
