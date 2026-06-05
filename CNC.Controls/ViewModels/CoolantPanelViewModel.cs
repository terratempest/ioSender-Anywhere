using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class CoolantPanelViewModel
{
    readonly MachineCommandService _commands;

    public CoolantPanelViewModel(MachineCommandService? commands = null)
    {
        _commands = commands ?? new MachineCommandService();
    }

    public GrblViewModel? Model { get; set; }

    public bool HasFans => Model?.HasFans == true;

    public void Toggle(string action)
    {
        if (Model == null)
            return;

        var command = action switch
        {
            "Flood" => GrblCommand.Flood,
            "Mist" => GrblCommand.Mist,
            "Fan" => GrblCommand.Fan,
            _ => null
        };

        if (command != null)
            _commands.ExecuteCommand(Model, command);
    }
}
