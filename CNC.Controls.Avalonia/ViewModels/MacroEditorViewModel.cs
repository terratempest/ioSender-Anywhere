using System.Collections.ObjectModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.ViewModels;

public sealed class MacroEditorViewModel : ViewModelBase
{
    readonly ObservableCollection<Macro> _target;
    readonly ObservableCollection<Macro> _working;
    Macro? _selectedMacro;
    string _editorName = string.Empty;
    string _editorCode = string.Empty;
    bool _editorConfirmOnExecute = true;

    public MacroEditorViewModel(ObservableCollection<Macro> macros)
    {
        _target = macros ?? throw new ArgumentNullException(nameof(macros));
        _working = new ObservableCollection<Macro>(_target.Select(Clone));
        if (_working.Count > 0)
            SelectedMacro = _working[0];
        else
            UpdateState();
    }

    public ObservableCollection<Macro> Macros => _working;

    public Macro? SelectedMacro
    {
        get => _selectedMacro;
        set
        {
            _selectedMacro = value;
            LoadSelectedMacro();
            OnPropertyChanged();
            UpdateState();
        }
    }

    public string EditorName
    {
        get => _editorName;
        set
        {
            _editorName = value ?? string.Empty;
            if (_selectedMacro != null)
                _selectedMacro.Name = _editorName;
            OnPropertyChanged();
            UpdateState();
        }
    }

    public string EditorCode
    {
        get => _editorCode;
        set
        {
            _editorCode = value ?? string.Empty;
            if (_selectedMacro != null)
                _selectedMacro.Code = TrimCode(_editorCode);
            OnPropertyChanged();
        }
    }

    public bool EditorConfirmOnExecute
    {
        get => _editorConfirmOnExecute;
        set
        {
            _editorConfirmOnExecute = value;
            if (_selectedMacro != null)
                _selectedMacro.ConfirmOnExecute = value;
            OnPropertyChanged();
        }
    }

    public bool HasSelection { get; private set; }

    public bool CanAdd { get; private set; }

    public bool CanDelete { get; private set; }

    public void AddMacro()
    {
        var id = NextAvailableId();
        if (id == 0)
            return;

        var macro = new Macro
        {
            Id = id,
            Name = $"Macro {id}",
            ConfirmOnExecute = true,
            Code = string.Empty,
        };

        _working.Add(macro);
        SelectedMacro = macro;
        OnPropertyChanged(nameof(Macros));
    }

    public void DeleteSelected()
    {
        if (_selectedMacro == null)
            return;

        var index = _working.IndexOf(_selectedMacro);
        _working.Remove(_selectedMacro);
        SelectedMacro = _working.Count == 0
            ? null
            : _working[Math.Clamp(index, 0, _working.Count - 1)];
        OnPropertyChanged(nameof(Macros));
    }

    public void Commit()
    {
        _target.Clear();
        foreach (var macro in _working.OrderBy(m => m.Id))
        {
            if (string.IsNullOrWhiteSpace(macro.Name) && string.IsNullOrWhiteSpace(macro.Code))
                continue;

            var copy = Clone(macro);
            copy.Code = TrimCode(copy.Code);
            _target.Add(copy);
        }
    }

    static Macro Clone(Macro macro) =>
        new()
        {
            Id = macro.Id,
            Name = macro.Name,
            ConfirmOnExecute = macro.ConfirmOnExecute,
            Code = macro.Code,
            IsSession = macro.IsSession,
        };

    static string TrimCode(string? code) => (code ?? string.Empty).TrimEnd('\r', '\n');

    int NextAvailableId()
    {
        for (var id = 1; id <= 12; id++)
        {
            if (_working.All(m => m.Id != id))
                return id;
        }

        return 0;
    }

    void LoadSelectedMacro()
    {
        _editorName = _selectedMacro?.Name ?? string.Empty;
        _editorCode = _selectedMacro?.Code ?? string.Empty;
        _editorConfirmOnExecute = _selectedMacro?.ConfirmOnExecute ?? true;
        OnPropertyChanged(nameof(EditorName));
        OnPropertyChanged(nameof(EditorCode));
        OnPropertyChanged(nameof(EditorConfirmOnExecute));
    }

    void UpdateState()
    {
        HasSelection = _selectedMacro != null;
        CanDelete = _selectedMacro != null;
        CanAdd = _working.Count < 12 && NextAvailableId() != 0;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanAdd));
    }
}
