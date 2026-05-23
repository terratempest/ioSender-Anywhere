using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

/// <summary>
/// Post-connect controller setup shared by ioSender and GrblConfigApp.
/// </summary>
public sealed class MachineConnectionInitializer
{
    const int MaxInfoRetries = 5;
    const int RetryDelayMs = 500;

    Action<string>? _grblResetHandler;
    GrblViewModel? _model;
    int _pollInterval;

    public bool Initialize(GrblViewModel model, int pollInterval)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (Comms.com is not { IsOpen: true })
        {
            model.Message = "Not connected.";
            CleanupAfterFailedInit(model);
            return false;
        }

        _model = model;
        _pollInterval = pollInterval;

        UnregisterGrblResetHandler();
        _grblResetHandler = OnGrblReset;
        model.OnGrblReset += _grblResetHandler;

        model.IsReady = false;
        model.Poller.SetState(0);
        model.Message = "Initializing controller...";

        try
        {
            Comms.com.PurgeQueue();

            var startupResponse = GrblInfo.Startup(model);
            if (!startupResponse.StartsWith("<", StringComparison.Ordinal))
            {
                model.Message = "No status response from controller.";
                CleanupAfterFailedInit(model);
                return false;
            }

            var retries = MaxInfoRetries;
            while (!GrblInfo.Get(model))
            {
                if (--retries <= 0)
                {
                    model.Message = "No response from controller.";
                    CleanupAfterFailedInit(model);
                    return false;
                }

                Thread.Sleep(RetryDelayMs);
            }

            GrblAlarms.Get();
            GrblErrors.Get();
            GrblSettings.Load(model);

            if (GrblInfo.IsGrblHAL)
            {
                GrblParserState.Get(model);
                GrblWorkParameters.Get(model);
                GrblSpindles.Get(model);
            }
            else
            {
                GrblSpindles.AddDefault();
                GrblParserState.Get(true);
            }

            GrblCommand.ToolChange = GrblInfo.ManualToolChange
                ? "M61Q{0}"
                : GrblInfo.HasATC ? "T{0}M6" : "T{0}";

            ResumePolling();
            var gotStatus = RequestInitialStatusSnapshot(model);
            model.IsReady = true;
            if (!model.IsDroPositionKnown)
            {
                model.Message = gotStatus
                    ? "Connected — no position in status report (check $10 / poll interval)."
                    : "Connected — no status report; DRO may stay blank until polling.";
            }
            else
                model.Message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            model.Message = ex.Message;
            CleanupAfterFailedInit(model);
            return false;
        }
    }

    public void Unregister()
    {
        UnregisterGrblResetHandler();
        _model = null;
    }

    void OnGrblReset(string _)
    {
        if (_model == null)
            return;

        ResumePolling();
        RequestImmediateStatusSnapshot();
    }

    void ResumePolling()
    {
        if (_model != null && _pollInterval > 0)
            _model.Poller.SetState(_pollInterval);
    }

    static void RequestImmediateStatusSnapshot()
    {
        if (Comms.com is not { IsOpen: true })
            return;

        var cmd = GrblInfo.IsGrblHAL
            ? GrblConstants.CMD_STATUS_REPORT_ALL
            : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL);
        Comms.com.WriteByte(cmd);
    }

    static bool RequestInitialStatusSnapshot(GrblViewModel model)
    {
        if (Comms.com is not { IsOpen: true })
            return false;

        var cmd = GrblInfo.IsGrblHAL
            ? GrblConstants.CMD_STATUS_REPORT_ALL
            : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL);

        if (TryRequestStatus(model, cmd))
            return true;

        var legacyStatus = (byte)GrblConstants.CMD_STATUS_REPORT_LEGACY[0];
        return cmd == legacyStatus || TryRequestStatus(model, legacyStatus);
    }

    static bool TryRequestStatus(GrblViewModel model, byte command)
    {
        bool? received = null;
        using var cancellation = new CancellationTokenSource();

        new Thread(() =>
        {
            received = WaitFor.SingleEvent<string>(
                cancellation.Token,
                null,
                a => model.OnRealtimeStatusProcessed += a,
                a => model.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(model.OnRealtimeStatusProcessed, a)!,
                500,
                () => Comms.com?.WriteByte(command));
        }).Start();

        while (received == null)
            EventUtils.DoEvents();

        return received == true;
    }

    static void CleanupAfterFailedInit(GrblViewModel model)
    {
        model.Poller.SetState(0);
        PollGrbl.Resume();
        model.IsReady = false;
        model.Silent = false;
    }

    void UnregisterGrblResetHandler()
    {
        if (_model == null || _grblResetHandler == null)
            return;

        _model.OnGrblReset = (Action<string>)Delegate.Remove(_model.OnGrblReset, _grblResetHandler)!;
        _grblResetHandler = null;
    }
}
