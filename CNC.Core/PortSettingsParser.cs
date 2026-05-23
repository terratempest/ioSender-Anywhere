using System.IO.Ports;

namespace CNC.Core;

public sealed class SerialPortSettings
{
    public required string PortName { get; init; }
    public int BaudRate { get; init; }
    public Parity Parity { get; init; }
    public int DataBits { get; init; }
    public StopBits StopBits { get; init; }
    public Handshake Handshake { get; init; }
    public Comms.ResetMode ResetMode { get; init; }
}

public static class PortSettingsParser
{
    public static SerialPortSettings Parse(string portParams)
    {
        if (string.IsNullOrWhiteSpace(portParams))
            throw new SerialPortConfigurationException("Port parameters cannot be empty.");

        if (!portParams.Contains(':'))
            portParams += ":115200,N,8,1";

        var colon = portParams.IndexOf(':');
        var portName = portParams[..colon];
        var parameter = portParams[(colon + 1)..].Split(',');

        if (parameter.Length < 4)
            throw new SerialPortConfigurationException($"Invalid serial port parameters: {portParams}");

        if (string.IsNullOrWhiteSpace(portName))
            throw new SerialPortConfigurationException($"Invalid serial port name in: {portParams}");

        if (!int.TryParse(parameter[0], out var baudRate))
            throw new SerialPortConfigurationException($"Invalid baud rate in: {portParams}");

        if (!int.TryParse(parameter[2], out var dataBits))
            throw new SerialPortConfigurationException($"Invalid data bits in: {portParams}");

        if (!int.TryParse(parameter[3], out var stopBitsValue) || (stopBitsValue != 1 && stopBitsValue != 2))
            throw new SerialPortConfigurationException($"Invalid stop bits in: {portParams}");

        var handshake = Handshake.None;
        if (parameter.Length > 4)
        {
            handshake = parameter[4] switch
            {
                "P" => Handshake.RequestToSend,
                "X" => Handshake.XOnXOff,
                _ => Handshake.None
            };
        }

        var resetMode = Comms.ResetMode.None;
        if (parameter.Length > 5)
            Enum.TryParse(parameter[5], true, out resetMode);

        return new SerialPortSettings
        {
            PortName = portName,
            BaudRate = baudRate,
            Parity = ParseParity(parameter[1]),
            DataBits = dataBits,
            StopBits = stopBitsValue == 1 ? StopBits.One : StopBits.Two,
            Handshake = handshake,
            ResetMode = resetMode
        };
    }

    private static Parity ParseParity(string parity) =>
        parity switch
        {
            "E" => Parity.Even,
            "O" => Parity.Odd,
            "M" => Parity.Mark,
            "S" => Parity.Space,
            _ => Parity.None
        };
}
