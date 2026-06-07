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
    private readonly AppSession _session;
    private readonly MainWindowViewModel _viewModel;
    private readonly ConnectionService _connectionService;
    private readonly MachineConnectionCoordinator _connectionCoordinator;
    private readonly MachineConnectionInitializer _connectionInitializer;
    private readonly ProgramService _programService;
    private CameraWindow? _cameraWindow;
    private Window? _sdCardWindow;
    private Window? _latheWizardsWindow;
    private Window? _viewerPreviewWindow;
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
        WindowDecorations = OperatingSystem.IsWindows()
            ? WindowDecorations.Full
            : WindowDecorations.None;
        if (OperatingSystem.IsLinux())
            TitleBar.PointerPressed += OnTitleBarPointerPressed;
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
        PopupKeyboardService.Attach(this);
        InitializeQuickAccessSidebar();
        WireShellTabHeaders();
        MnuView.SubmenuOpened += (_, _) =>
        {
            UpdateLayoutMenuEnabled();
            UpdateFloatingPanelMenuEnabled();
        };
        MnuLayouts.SubmenuOpened += (_, _) => RebuildLayoutsMenu();
        Opened += OnMainWindowOpened;
        Activated += (_, _) => _session.GameController.IsApplicationActive = true;
        Deactivated += (_, _) => _session.GameController.IsApplicationActive = false;
        Closing += OnMainWindowClosing;
        PositionChanged += (_, _) => SaveWindowPlacement();
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

}
