using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public sealed class MachineConnectionCoordinator
{
    private readonly ConnectionService _connection;
    private GrblViewModel? _attachedModel;

    public MachineConnectionCoordinator(ConnectionService connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public ConnectionService Connection => _connection;

    public bool IsAttachedTo(GrblViewModel model) =>
        _attachedModel != null && ReferenceEquals(_attachedModel, model) &&
        _connection.Stream is { IsOpen: true };

    /// <summary>
    /// Wires the active stream to the view model. Does not start polling or set <see cref="GrblViewModel.IsReady"/>.
    /// </summary>
    public bool AttachAfterConnect(GrblViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var stream = _connection.Stream;
        if (stream is not { IsOpen: true })
            return false;

        if (IsAttachedTo(model))
            return true;

        if (_attachedModel != null && _connection.Stream != null)
            _connection.Stream.DataReceived -= _attachedModel.DataReceived;

        Grbl.GrblViewModel = model;
        stream.DataReceived += model.DataReceived;
        _attachedModel = model;
        model.IsReady = false;
        return true;
    }

    public void Detach(GrblViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_connection.Stream != null && _attachedModel != null)
            _connection.Stream.DataReceived -= _attachedModel.DataReceived;

        _attachedModel = null;
        model.Poller.SetState(0);
        PollGrbl.Resume();
        model.IsReady = false;
        _connection.Disconnect();
    }
}
