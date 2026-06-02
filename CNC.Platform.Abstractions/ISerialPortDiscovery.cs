namespace CNC.Platform.Abstractions;

public interface ISerialPortDiscovery
{
    IReadOnlyList<SerialPortInfo> GetPorts();
    bool IsPortAvailable(string portName);
}
