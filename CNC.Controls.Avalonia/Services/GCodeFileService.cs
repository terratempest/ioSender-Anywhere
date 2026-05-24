using System.Collections.ObjectModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Services;

/// <summary>Platform-neutral G-code file holder (replaces legacy WPF GCode.File).</summary>
public sealed class GCodeFileService
{
    public const string FileTypes = "cnc,nc,ncc,ngc,gcode,tap";

    private static readonly Lazy<GCodeFileService> InstanceHolder = new(() => new GCodeFileService());

    private readonly GCodeJob _program = new();
    private Dialect? _savedLoadDialect;

    private GCodeFileService()
    {
        _program.FileChanged += OnProgramFileChanged;
    }

    public static GCodeFileService Instance => InstanceHolder.Value;

    public GrblViewModel? Model { get; set; }

    public bool IsLoaded => _program.Loaded;

    public ObservableCollection<GCodeBlock> Data => _program.Blocks;

    public int Blocks => _program.Blocks.Count;

    public List<GCodeToken> Tokens => _program.Tokens;

    public GCodeParser Parser => _program.Parser;

    public bool HeightMapApplied
    {
        get => _program.HeightMapApplied;
        set => _program.HeightMapApplied = value;
    }

    /// <summary>Fired before program data is mutated; detach UI bound to <see cref="Data"/>.</summary>
    public event System.Action? ProgramLoading;

    public event System.Action? ProgramChanged;

    public void Load(string filename)
    {
        BeginProgramMutation();
        try
        {
            RunWithLoadDialect(() => _program.LoadFile(filename, GrblInfo.UseLinenumbers));
        }
        catch (Exception ex)
        {
            GrblUi.ShowError(ex.Message, "ioSender");
            return;
        }

        if (Model != null)
        {
            Model.FileName = filename;
            Model.Blocks = Blocks;
        }

        ProgramChanged?.Invoke();
    }

    /// <summary>Load generated or pasted G-code into the active program.</summary>
    public void LoadFromLines(IEnumerable<string> lines, string displayName)
    {
        ArgumentNullException.ThrowIfNull(lines);

        BeginProgramMutation();
        RunWithLoadDialect(() =>
        {
            _program.AddBlock(displayName, Core.Action.New);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _program.AddBlock(line.Trim(), Core.Action.Add);
            }

            _program.AddBlock(string.Empty, Core.Action.End);
            return true;
        });

        if (Model != null)
        {
            Model.FileName = displayName;
            Model.Blocks = Blocks;
        }

        ProgramChanged?.Invoke();
    }

    public void Close()
    {
        BeginProgramMutation();
        _program.CloseFile();
        if (Model != null)
        {
            Model.FileName = string.Empty;
            Model.Blocks = Blocks;
        }
        ProgramChanged?.Invoke();
    }

    public void AddBlock(string block) => _program.AddBlock(block);

    public void AddBlock(string block, CNC.Core.Action action)
    {
        if (action == CNC.Core.Action.New)
            BeginLoadDialect();

        _program.AddBlock(block, action);

        if (action == CNC.Core.Action.End)
            EndLoadDialect();

        if (action == CNC.Core.Action.End)
            NotifyProgramChanged();
    }

    void NotifyProgramChanged()
    {
        if (Model != null)
            Model.Blocks = Blocks;
        ProgramChanged?.Invoke();
    }

    public void ReplaceFromTokens(IReadOnlyList<GCodeToken> tokens, string headerComment, bool compress)
    {
        BeginProgramMutation();
        var gc = GCodeParser.TokensToGCode(tokens.ToList(), compress);
        var fileName = Model?.FileName ?? string.Empty;
        AddBlock($"{headerComment}: {fileName}", CNC.Core.Action.New);
        foreach (var block in gc)
            AddBlock(block, CNC.Core.Action.Add);
        AddBlock(string.Empty, CNC.Core.Action.End);
    }

    private void OnProgramFileChanged(string filename)
    {
        if (Model == null)
            return;

        if (filename == "")
        {
            Model.ProgramLimits.Clear();
            return;
        }

        foreach (int i in AxisFlags.All.ToIndices())
        {
            Model.ProgramLimits.MinValues[i] = Model.ConvertMM2Current(_program.BoundingBox.Min[i]);
            Model.ProgramLimits.MaxValues[i] = Model.ConvertMM2Current(_program.BoundingBox.Max[i]);
        }
    }

    void BeginProgramMutation() => ProgramLoading?.Invoke();

    private bool RunWithLoadDialect(Func<bool> load)
    {
        BeginLoadDialect();
        try
        {
            return load();
        }
        finally
        {
            EndLoadDialect();
        }
    }

    private void BeginLoadDialect()
    {
        _savedLoadDialect ??= Parser.Dialect;
        Parser.Dialect = Dialect.GrblHAL;
    }

    private void EndLoadDialect()
    {
        if (_savedLoadDialect is not { } dialect)
            return;
        Parser.Dialect = dialect;
        _savedLoadDialect = null;
    }
}
