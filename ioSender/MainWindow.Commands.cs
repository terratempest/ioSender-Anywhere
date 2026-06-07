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
