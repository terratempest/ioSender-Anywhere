using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

/// <summary>Single UI-facing route for controller commands and realtime bytes.</summary>
public sealed class MachineCommandService
{
    readonly GrblCommandRouter _router;

    public MachineCommandService(GrblCommandRouter? router = null)
    {
        _router = router ?? new GrblCommandRouter();
    }

    public GrblCommandRouter Router => _router;

    public void Attach(GrblViewModel model) => _router.Attach(model);

    public void Detach() => _router.Detach();

    public void SendCommand(string command) => _router.Send(command);

    public void ExecuteCommand(GrblViewModel model, string command)
    {
        model.ExecuteCommand(command);
    }

    public void SendRealtime(byte command) =>
        Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(command));

    public void SendRealtime(char command)
    {
        if (Comms.com == null)
            return;

        var value = (int)command;
        if (value > byte.MaxValue)
        {
            value = value switch
            {
                8222 => GrblConstants.CMD_SAFETY_DOOR,
                8225 => GrblConstants.CMD_STATUS_REPORT_ALL,
                710 => GrblConstants.CMD_OPTIONAL_STOP_TOGGLE,
                8240 => GrblConstants.CMD_SINGLE_BLOCK_TOGGLE,
                _ => value
            };
        }

        if (value <= byte.MaxValue)
            Comms.com.WriteByte(GrblLegacy.ConvertRTCommand((byte)value));
    }

    public void Reset() => SendRealtime(GrblConstants.CMD_RESET);

    public void StatusReport() => SendRealtime(GrblConstants.CMD_STATUS_REPORT);

    public void StatusReportAll() => SendRealtime(GrblConstants.CMD_STATUS_REPORT_ALL);
}
