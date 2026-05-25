using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class UiServiceBoundaryTests
{
    [Fact]
    public void MachineCommandService_RealtimeNoOpsWhenDisconnected()
    {
        var service = new MachineCommandService();

        service.SendRealtime(GrblConstants.CMD_STATUS_REPORT);
        service.SendRealtime('?');
        service.SendCommand("G0 X0");
    }
}
