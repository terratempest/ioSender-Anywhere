using System.Globalization;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class FeedPanelViewModel
{
    readonly MachineCommandService _commands;

    public FeedPanelViewModel(MachineCommandService? commands = null)
    {
        _commands = commands ?? new MachineCommandService();
    }

    public GrblViewModel? Model { get; set; }

    public bool UseCoarseFeedStep { get; set; }

    public string FeedRateText { get; private set; } = string.Empty;

    public string FeedOverrideSummary { get; private set; } = string.Empty;

    public string FeedOverrideText { get; private set; } = string.Empty;

    public string RapidsOverrideText { get; private set; } = string.Empty;

    public void UpdateFromModel()
    {
        if (Model == null)
            return;

        FeedRateText = Model.FeedRate.ToString("####0", CultureInfo.InvariantCulture);
        FeedOverrideSummary = $"{Model.FeedrateUnit} % {Model.FeedOverride:0}";
        FeedOverrideText = $"Feed % {Model.FeedOverride:0}";
        RapidsOverrideText = $"%{Model.RapidsOverride:0}";
    }

    public void FeedOverridePlus() =>
        Send(UseCoarseFeedStep ? GrblConstants.CMD_FEED_OVR_COARSE_PLUS : GrblConstants.CMD_FEED_OVR_FINE_PLUS);

    public void FeedOverrideMinus() =>
        Send(UseCoarseFeedStep ? GrblConstants.CMD_FEED_OVR_COARSE_MINUS : GrblConstants.CMD_FEED_OVR_FINE_MINUS);

    public void FeedOverrideReset() => Send(GrblConstants.CMD_FEED_OVR_RESET);

    public void RapidsOverridePlus()
    {
        if (Model == null)
            return;

        if (Model.RapidsOverride <= 25)
            Send(GrblConstants.CMD_RAPID_OVR_MEDIUM);
        else if (Model.RapidsOverride <= 50)
            Send(GrblConstants.CMD_RAPID_OVR_RESET);
    }

    public void RapidsOverrideMinus()
    {
        if (Model == null)
            return;

        if (Model.RapidsOverride > 50)
            Send(GrblConstants.CMD_RAPID_OVR_MEDIUM);
        else if (Model.RapidsOverride > 25)
            Send(GrblConstants.CMD_RAPID_OVR_LOW);
    }

    public void RapidsOverrideReset() => Send(GrblConstants.CMD_RAPID_OVR_RESET);

    public void SetRapidsOverride(int value)
    {
        var command = value switch
        {
            25 => GrblConstants.CMD_RAPID_OVR_LOW,
            50 => GrblConstants.CMD_RAPID_OVR_MEDIUM,
            100 => GrblConstants.CMD_RAPID_OVR_RESET,
            _ => (byte)0
        };

        if (command != 0)
            Send(command);
    }

    public void SendOverrideCommands(byte[] commands, int len)
    {
        for (var i = 0; i < len; i++)
            _commands.SendRealtime(commands[i]);

        if (len > 0)
            _commands.StatusReport();
    }

    void Send(byte command)
    {
        _commands.SendRealtime(command);
        _commands.StatusReport();
    }
}
