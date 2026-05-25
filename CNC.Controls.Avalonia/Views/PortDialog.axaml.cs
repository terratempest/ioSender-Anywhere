using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Localization.Avalonia;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.Views;

public partial class PortDialog : Window
{
    public PortDialog()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public PortDialog(
        ISerialPortDiscovery portDiscovery,
        MachineConnectionCoordinator coordinator,
        GrblViewModel grbl,
        int pollInterval,
        int resetDelay = 0,
        string? savedPortParams = null)
    {
        InitializeComponent();
        ApplyLocalization();
        DataContext = new PortDialogViewModel(
            portDiscovery, coordinator, grbl, pollInterval, resetDelay, savedPortParams);
        PortCombo.DropDownOpened += (_, _) => ViewModel.RefreshPorts();
    }

    public bool Connected { get; private set; }

    public string? ConnectedPortParams { get; private set; }

    private PortDialogViewModel ViewModel => (PortDialogViewModel)DataContext!;

    private void ApplyLocalization()
    {
        Localize.Set(PortDialogRoot, "CNC.Controls.Avalonia.portdialog.dlg_title", "Sender connection");
        Localize.Apply(PortDialogRoot);
        Localize.Apply(TabSerial);
        Localize.Apply(TabTelnet);
        Localize.Apply(TabWebsocket);
        Localize.Apply(LblSerialPort);
        Localize.Apply(LblSerialBaud);
        Localize.Apply(LblTelnetHost);
        Localize.Apply(LblTelnetPort);
        Localize.Set(BtnConnect, "CNC.Controls.Avalonia.portdialog.btn_ok", "Connect");
        Localize.Apply(BtnConnect);
        Localize.Apply(BtnCancel);
    }

    private void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.TryConnect())
            return;

        Connected = true;
        ConnectedPortParams = ViewModel.ConnectedPortParams;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Connected = false;
        Close(false);
    }
}
