using System.Globalization;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class SpindlePanelViewModel
{
    readonly MachineCommandService _commands;

    public SpindlePanelViewModel(MachineCommandService? commands = null)
    {
        _commands = commands ?? new MachineCommandService();
    }

    public GrblViewModel? Model { get; set; }

    public bool UseCoarseOverrideStep { get; set; }

    public bool IsSpindleStateEnabled { get; private set; }

    public string RpmText { get; private set; } = string.Empty;

    public void UpdateSpindleStateEnabled()
    {
        IsSpindleStateEnabled = Model != null
            && (!Model.IsJobRunning || Model.GrblState.State is GrblStates.Hold or GrblStates.Door);
    }

    public bool UpdateRpmText(bool force, bool rpmTextIsFocused)
    {
        if (Model == null || (!force && rpmTextIsFocused))
            return false;

        RpmText = Model.SpindleSetpointRPM.ToString("####0", CultureInfo.InvariantCulture);
        return true;
    }

    public bool TrySetRpm(string? text, out string normalizedRpmText)
    {
        normalizedRpmText = string.Empty;
        if (Model == null || !TryParseRpm(text, out var rpm))
            return false;

        Model.SpindleSetpointRPM = rpm;
        normalizedRpmText = rpm.ToString("####0", CultureInfo.InvariantCulture);
        _commands.ExecuteCommand(Model, "S" + rpm.ToInvariantString());
        return true;
    }

    public void SelectSpindleState(string commandTemplate)
    {
        if (Model == null)
            return;

        if (Model.GrblState.State == GrblStates.Hold)
        {
            _commands.SendRealtime(GrblConstants.CMD_SPINDLE_OVR_STOP);
            return;
        }

        var rpm = Model.ProgrammedRPM == 0d ? "S" + Model.SpindleSetpointRPM.ToInvariantString() : "";
        _commands.ExecuteCommand(Model, string.Format(commandTemplate, rpm));
    }

    public void ChangeSpindle(Spindle spindle)
    {
        if (Model == null)
            return;

        if (Model.GrblError != 0)
            _commands.ExecuteCommand(Model, "");

        if (GrblInfo.IsGrblHAL && GrblInfo.Build < 20240812)
            _commands.ExecuteCommand(Model, string.Format(GrblCommand.SpindleChange, spindle.SpindleId));
        else
            _commands.ExecuteCommand(Model, string.Format(GrblCommand.SpindleChange, spindle.SpindleNum));
    }

    public void OverridePlus() =>
        Send(UseCoarseOverrideStep ? GrblConstants.CMD_SPINDLE_OVR_COARSE_PLUS : GrblConstants.CMD_SPINDLE_OVR_FINE_PLUS);

    public void OverrideMinus() =>
        Send(UseCoarseOverrideStep ? GrblConstants.CMD_SPINDLE_OVR_COARSE_MINUS : GrblConstants.CMD_SPINDLE_OVR_FINE_MINUS);

    public void OverrideReset() => Send(GrblConstants.CMD_SPINDLE_OVR_RESET);

    static bool TryParseRpm(string? text, out double rpm)
    {
        var value = text?.Trim() ?? string.Empty;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out rpm)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out rpm);
    }

    public void SendOverride(byte command)
    {
        _commands.SendRealtime(command);
        _commands.StatusReport();
    }

    public void SendOverrideCommands(byte[] commands, int len)
    {
        for (var i = 0; i < len; i++)
            _commands.SendRealtime(commands[i]);

        if (len > 0)
            _commands.StatusReport();
    }

    public bool TrySetRpmFromValue(double rpm)
    {
        if (Model == null || rpm < 0d)
            return false;

        Model.RPM = rpm;
        _commands.ExecuteCommand(Model, "S" + rpm.ToInvariantString());
        return true;
    }

    void Send(byte command)
    {
        _commands.SendRealtime(command);
        _commands.StatusReport();
    }
}
