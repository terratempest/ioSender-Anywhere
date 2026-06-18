using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class FileActionViewModel
{
    readonly ProgramService _program;
    readonly IExternalEditor? _externalEditor;
    readonly bool _ownsProgramModel;
    GrblViewModel? _model;

    public FileActionViewModel(ProgramService? program = null, IExternalEditor? externalEditor = null)
    {
        _ownsProgramModel = program is null;
        _program = program ?? new ProgramService();
        _externalEditor = externalEditor;
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

    public void Open(string path)
    {
        if (CanMutateProgram() && !string.IsNullOrEmpty(path))
            _program.Load(path);
    }

    public void Reload()
    {
        if (CanMutateProgram() && !string.IsNullOrEmpty(Model?.FileName))
            _program.Load(Model.FileName);
    }

    public void Close()
    {
        if (CanMutateProgram())
            _program.Close();
    }

    public async Task EditAsync()
    {
        if (CanMutateProgram() && !string.IsNullOrEmpty(Model?.FileName) && _externalEditor != null)
            await _externalEditor.OpenFileAsync(Model.FileName);
    }

    bool CanMutateProgram() => Model is not { IsJobRunning: true } and not { IsToolChanging: true };
}
