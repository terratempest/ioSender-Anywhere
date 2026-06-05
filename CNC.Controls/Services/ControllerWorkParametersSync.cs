using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

/// <summary>Re-sync NGC work parameters and parser state from the controller (e.g. after M6 or reset).</summary>
public static class ControllerWorkParametersSync
{
    public static void Refresh(GrblViewModel? model)
    {
        if (model == null || !GrblInfo.IsGrblHAL || Comms.com is not { IsOpen: true })
            return;

        GrblWorkParameters.Get(model);
        GrblParserState.Get(model);
    }
}
