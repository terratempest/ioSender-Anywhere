using System.ComponentModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Services;

/// <summary>
/// Sends MDI and single-byte realtime commands from <see cref="GrblViewModel"/> without requiring JobControl in the layout.
/// </summary>
public sealed class GrblCommandRouter
{
    readonly Queue<string> _pending = new();
    GrblViewModel? _model;
    bool _awaitingResponse;

    public void Attach(GrblViewModel model)
    {
        if (ReferenceEquals(_model, model))
            return;

        Detach();
        _model = model;
        model.PropertyChanged += OnModelPropertyChanged;
        model.OnCommandResponseReceived += OnCommandResponse;
    }

    public void Detach()
    {
        if (_model == null)
            return;

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.OnCommandResponseReceived = (Action<string>)Delegate.Remove(_model.OnCommandResponseReceived, OnCommandResponse)!;
        _model = null;
        _pending.Clear();
        _awaitingResponse = false;
    }

    void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.MDI) && sender is GrblViewModel vm)
            Send(vm.MDI);
    }

    void OnCommandResponse(string response)
    {
        if (!_awaitingResponse)
            return;

        if (response == "ok" || response.StartsWith("error", StringComparison.OrdinalIgnoreCase))
        {
            _awaitingResponse = false;
            FlushPending();
        }
    }

    public void Send(string command)
    {
        if (string.IsNullOrEmpty(command) || Comms.com is not { IsOpen: true })
            return;

        if (command.Length == 1)
        {
            SendRealtime(command[0]);
            return;
        }

        try
        {
            var parsed = command;
            GCodeFileService.Instance.Parser.ParseBlock(ref parsed, true);
            _pending.Enqueue(command);
            if (!_awaitingResponse)
                FlushPending();
        }
        catch
        {
            // Invalid block — GrblViewModel already validated via ApplyCommand for UI paths.
        }
    }

    void FlushPending()
    {
        if (_pending.Count == 0 || Comms.com is not { IsOpen: true })
            return;

        _awaitingResponse = true;
        Comms.com.WriteCommand(_pending.Dequeue());
    }

    static void SendRealtime(char command)
    {
        if (Comms.com is not { IsOpen: true })
            return;

        var b = (int)command;
        if (b > 255)
        {
            b = b switch
            {
                8222 => GrblConstants.CMD_SAFETY_DOOR,
                8225 => GrblConstants.CMD_STATUS_REPORT_ALL,
                710 => GrblConstants.CMD_OPTIONAL_STOP_TOGGLE,
                8240 => GrblConstants.CMD_SINGLE_BLOCK_TOGGLE,
                _ => b
            };
        }

        if (b <= 255)
            Comms.com.WriteByte(GrblLegacy.ConvertRTCommand((byte)b));
    }
}
