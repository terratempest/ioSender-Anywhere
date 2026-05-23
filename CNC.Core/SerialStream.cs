/*
 * SerialStream.cs - part of CNC Controls library
 *
 * v0.41 / 2022-09-25 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2022, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

· Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

· Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

· Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System.IO.Ports;
using System.Text;
using CNC.Platform.Abstractions;

namespace CNC.Core;

public class SerialStream : StreamComms
{
    private SerialPort? serialPort;
    private readonly StringBuilder input = new(Comms.RXBUFFERSIZE);
    private volatile Comms.State state = Comms.State.ACK;
    private readonly IUiDispatcher _uiDispatcher;

    public event DataReceivedHandler? DataReceived;

    public SerialStream(
        string portParams,
        int resetDelay,
        ISerialPortDiscovery portDiscovery,
        IUiDispatcher uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(portDiscovery);
        ArgumentNullException.ThrowIfNull(uiDispatcher);

        Comms.com = this;
        Comms.UiDispatcher = uiDispatcher;
        _uiDispatcher = uiDispatcher;
        Reply = string.Empty;

        var settings = PortSettingsParser.Parse(portParams);
        EnsurePortAvailable(settings.PortName, portDiscovery);

        serialPort = new SerialPort
        {
            PortName = settings.PortName,
            BaudRate = settings.BaudRate,
            Parity = settings.Parity,
            DataBits = settings.DataBits,
            StopBits = settings.StopBits,
            Handshake = settings.Handshake,
            ReceivedBytesThreshold = 1,
            ReadTimeout = 50,
            ReadBufferSize = Comms.RXBUFFERSIZE,
            WriteBufferSize = Comms.TXBUFFERSIZE
        };

        try
        {
            serialPort.Open();
        }
        catch (Exception ex)
        {
            throw new SerialPortConfigurationException(
                $"Unable to open serial port '{settings.PortName}'.", ex);
        }

        if (!serialPort.IsOpen)
            throw new SerialPortConfigurationException($"Serial port '{settings.PortName}' did not open.");

        serialPort.DtrEnable = true;
        PurgeQueue();
        serialPort.DataReceived += SerialPort_DataReceived;

        switch (settings.ResetMode)
        {
            case Comms.ResetMode.RTS:
                serialPort.RtsEnable = true;
                Thread.Sleep(5);
                if (resetDelay > 0)
                    Thread.Sleep(resetDelay);
                break;

            case Comms.ResetMode.DTR:
                serialPort.DtrEnable = false;
                Thread.Sleep(5);
                serialPort.DtrEnable = true;
                if (resetDelay > 0)
                    Thread.Sleep(resetDelay);
                break;
        }
    }

    ~SerialStream()
    {
        if (!IsClosing && IsOpen)
            Close();
    }

    public Comms.StreamType StreamType => Comms.StreamType.Serial;
    public Comms.State CommandState { get => state; set => state = value; }
    public string Reply { get; private set; } = string.Empty;
    public bool IsOpen => serialPort != null && serialPort.IsOpen;
    public bool IsClosing { get; private set; }
    public int OutCount => serialPort?.BytesToWrite ?? 0;
    public bool EventMode { get; set; } = true;
    public Action<int>? ByteReceived { get; set; }

    public void PurgeQueue()
    {
        if (serialPort != null)
        {
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
        }

        Reply = string.Empty;
        if (!EventMode)
            input.Clear();
    }

    public void Close()
    {
        if (IsClosing || !IsOpen)
            return;

        IsClosing = true;
        try
        {
            serialPort!.DataReceived -= SerialPort_DataReceived;
            serialPort.DtrEnable = false;
            serialPort.RtsEnable = false;
            serialPort.DiscardInBuffer();
            serialPort.DiscardOutBuffer();
            Thread.Sleep(100);
            serialPort.Close();
            serialPort = null;
        }
        catch
        {
        }
        finally
        {
            IsClosing = false;
        }
    }

    public int ReadByte()
    {
        if (input.Length == 0)
            return -1;

        var c = input[0];
        input.Remove(0, 1);
        return c;
    }

    public void WriteByte(byte data)
    {
        serialPort?.BaseStream.Write(new byte[1] { data }, 0, 1);
    }

    public void WriteBytes(byte[] bytes, int len)
    {
        serialPort?.BaseStream.WriteAsync(bytes, 0, len);
    }

    public void WriteString(string data)
    {
        byte[] bytes = Encoding.Default.GetBytes(data);
        WriteBytes(bytes, bytes.Length);
    }

    public void WriteCommand(string command)
    {
        state = Comms.State.AwaitAck;

        if (command.Length == 1 && command != GrblConstants.CMD_PROGRAM_DEMARCATION)
            WriteByte((byte)command[0]);
        else
        {
            command += "\r";
            byte[] bytes = Encoding.UTF8.GetBytes(command);
            serialPort?.BaseStream.Write(bytes, 0, bytes.Length);
        }
    }

    public void AwaitAck()
    {
        while (Comms.com!.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
            EventUtils.DoEvents();
    }

    public void AwaitAck(string command)
    {
        PurgeQueue();
        Reply = string.Empty;
        WriteCommand(command);

        while (Comms.com!.CommandState == Comms.State.DataReceived || Comms.com.CommandState == Comms.State.AwaitAck)
            EventUtils.DoEvents();
    }

    public void AwaitResponse()
    {
        while (Comms.com!.CommandState == Comms.State.AwaitAck)
            EventUtils.DoEvents();
    }

    public void AwaitResponse(string command)
    {
        PurgeQueue();
        Reply = string.Empty;
        WriteCommand(command);

        while (Comms.com!.CommandState == Comms.State.AwaitAck)
            Thread.Sleep(15);
    }

    public string GetReply(string command)
    {
        Reply = string.Empty;
        WriteCommand(command);
        AwaitResponse();
        return Reply;
    }

    private static void EnsurePortAvailable(string portName, ISerialPortDiscovery portDiscovery)
    {
        var known = portDiscovery.GetPorts();
        if (known.Count == 0)
            return;

        if (!known.Any(p => string.Equals(p.Name, portName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new SerialPortConfigurationException(
                $"Serial port '{portName}' was not found. Available: {string.Join(", ", known.Select(p => p.Name))}.");
        }
    }

    private int gp()
    {
        int pos = 0;
        bool found = false;

        while (!found && pos < input.Length)
            found = input[pos++] == '\n';

        return found ? pos - 1 : 0;
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        int pos;

        lock (input)
        {
            input.Append(serialPort!.ReadExisting());

            if (EventMode)
            {
                while (input.Length > 0 && (pos = gp()) > 0)
                {
                    Reply = pos == 0 ? string.Empty : input.ToString(0, pos - 1);
                    input.Remove(0, pos + 1);

                    if (Reply.Length != 0 && DataReceived != null)
                    {
                        var reply = Reply;
                        _uiDispatcher.Post(() => DataReceived(reply));
                    }

                    state = Reply == "ok" ? Comms.State.ACK : (Reply.StartsWith("error") ? Comms.State.NAK : Comms.State.DataReceived);
                }
            }
            else
                ByteReceived?.Invoke(ReadByte());
        }
    }
}
