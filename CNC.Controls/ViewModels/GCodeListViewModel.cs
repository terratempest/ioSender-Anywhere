using System.Collections.ObjectModel;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class GCodeListViewModel
{
    readonly ProgramService _program;
    readonly MachineCommandService _commands;
    readonly bool _ownsProgramModel;
    GrblViewModel? _model;

    public GCodeListViewModel(ProgramService? program = null, MachineCommandService? commands = null)
    {
        _ownsProgramModel = program is null;
        _program = program ?? new ProgramService();
        _commands = commands ?? new MachineCommandService();
    }

    public GrblViewModel? Model
    {
        get => _ownsProgramModel ? _program.Model : _model;
        set
        {
            _model = value;
            if (_ownsProgramModel)
                _program.Model = value;
        }
    }

    public ObservableCollection<GCodeBlock> Data => _program.Data;

    public event System.Action? ProgramLoading
    {
        add => _program.ProgramLoading += value;
        remove => _program.ProgramLoading -= value;
    }

    public event System.Action? ProgramChanged
    {
        add => _program.ProgramChanged += value;
        remove => _program.ProgramChanged -= value;
    }

    public bool CanStartFrom(int index) => Model?.StartFromBlock.CanExecute(index) == true;

    public void StartFrom(int index)
    {
        if (Model != null && index >= 0)
            Model.StartFromBlock.Execute(index);
    }

    public void CopyToMdi(GCodeBlock block)
    {
        if (Model != null)
            Model.MDIText = block.DisplayData;
    }

    public void ToggleBreak(GCodeBlock block) => block.BreakAt ^= true;

    public void SendToController(IEnumerable<GCodeBlock> blocks)
    {
        if (Model == null)
            return;

        foreach (var block in blocks.OrderBy(b => b.Row))
            _commands.ExecuteCommand(Model, block.Data);
    }

    public void LoadDroppedFile(string path)
    {
        if (!string.IsNullOrEmpty(path))
            _program.Load(path);
    }
}
