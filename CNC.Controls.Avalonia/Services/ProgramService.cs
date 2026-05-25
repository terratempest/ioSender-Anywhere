using System.Collections.ObjectModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Services;

/// <summary>Owns the active G-code program surface used by Avalonia UI.</summary>
public sealed class ProgramService
{
    readonly Lazy<GCodeFileService> _files;
    GrblViewModel? _model;

    public ProgramService(GCodeFileService? files = null)
    {
        _files = new Lazy<GCodeFileService>(() =>
        {
            var service = files ?? GCodeFileService.Instance;
            service.Model = _model;
            return service;
        });
    }

    GCodeFileService Files => _files.Value;

    public GrblViewModel? Model
    {
        get => _files.IsValueCreated ? Files.Model : _model;
        set
        {
            _model = value;
            if (_files.IsValueCreated)
                Files.Model = value;
        }
    }

    public bool IsLoaded => Files.IsLoaded;

    public ObservableCollection<GCodeBlock> Data => Files.Data;

    public int Blocks => Files.Blocks;

    public IReadOnlyList<GCodeToken> Tokens => Files.Tokens;

    public GCodeParser Parser => Files.Parser;

    public bool HeightMapApplied
    {
        get => Files.HeightMapApplied;
        set => Files.HeightMapApplied = value;
    }

    public event System.Action? ProgramLoading
    {
        add => Files.ProgramLoading += value;
        remove => Files.ProgramLoading -= value;
    }

    public event System.Action? ProgramChanged
    {
        add => Files.ProgramChanged += value;
        remove => Files.ProgramChanged -= value;
    }

    public void Load(string filename) => Files.Load(filename);

    public void LoadFromLines(IEnumerable<string> lines, string displayName) =>
        Files.LoadFromLines(lines, displayName);

    public void Close() => Files.Close();

    public void AddBlock(string block) => Files.AddBlock(block);

    public void AddBlock(string block, CNC.Core.Action action) => Files.AddBlock(block, action);

    public void ReplaceFromTokens(IReadOnlyList<GCodeToken> tokens, string headerComment, bool compress) =>
        Files.ReplaceFromTokens(tokens, headerComment, compress);
}
