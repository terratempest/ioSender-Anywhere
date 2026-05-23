using System.Collections.ObjectModel;

using System.Xml.Serialization;

using CNC.Controls.Avalonia.Services;

using CNC.Core;



namespace CNC.Controls.Probing;



public sealed class ProbeMacroViewModel : ViewModelBase

{

    const string MacrosFileName = "ProbingMacros.xml";

    static readonly string[] NoCommands = [];



    readonly ProbingMacros _probeMacros = new();



    ProbingMacro? _selectedMacro;

    string _preMacroText = string.Empty;

    string _postMacroText = string.Empty;

    string _newMacroName = string.Empty;

    bool _runOnce;



    public ProbeMacroViewModel()

    {

        _probeMacros.Load();

        SelectedMacro = _probeMacros.Macros.FirstOrDefault();

        AddCommand = new ActionCommand(AddOrUpdateMacro, () => CanAdd || CanEdit);

        DeleteCommand = new ActionCommand(DeleteSelectedMacro, () => CanDelete);

    }



    public ObservableCollection<ProbingMacro> Macros => _probeMacros.Macros;



    public ActionCommand AddCommand { get; }

    public ActionCommand DeleteCommand { get; }



    public string[] PreJobCommands =>

        _selectedMacro == null ? NoCommands : SplitCommands(_selectedMacro.PreCommands);



    public string[] PostJobCommands =>

        _selectedMacro == null ? NoCommands : SplitCommands(_selectedMacro.PostCommands);



    public bool CanAdd => _selectedMacro == null;



    public bool CanEdit => _selectedMacro is { Id: not 0 };



    public bool CanDelete => _selectedMacro is { Id: > 0 };



    public bool RunOnce

    {

        get => _runOnce;

        set { if (value == _runOnce) return; _runOnce = value; OnPropertyChanged(); }

    }



    public string PreMacroText

    {

        get => _preMacroText;

        set { if (value == _preMacroText) return; _preMacroText = value; OnPropertyChanged(); }

    }



    public string PostMacroText

    {

        get => _postMacroText;

        set { if (value == _postMacroText) return; _postMacroText = value; OnPropertyChanged(); }

    }



    /// <summary>Name for a new macro when <see cref="SelectedMacro"/> is null.</summary>

    public string NewMacroName

    {

        get => _newMacroName;

        set { _newMacroName = value; OnPropertyChanged(); }

    }



    public ProbingMacro? SelectedMacro

    {

        get => _selectedMacro;

        set

        {

            if (_selectedMacro == value)

                return;



            _selectedMacro = value;

            if (value == null)

            {

                RunOnce = false;

                PreMacroText = string.Empty;

                PostMacroText = string.Empty;

            }

            else

            {

                RunOnce = value.RunOnce;

                PreMacroText = value.PreCommands;

                PostMacroText = value.PostCommands;

            }



            OnPropertyChanged();

            OnPropertyChanged(nameof(CanAdd));

            OnPropertyChanged(nameof(CanEdit));

            OnPropertyChanged(nameof(CanDelete));

            OnPropertyChanged(nameof(PreJobCommands));

            OnPropertyChanged(nameof(PostJobCommands));

            OnPropertyChanged(nameof(ActiveMacroName));

        }

    }



    public string ActiveMacroName =>

        _selectedMacro == null || _selectedMacro.Id == 0 ? string.Empty : _selectedMacro.Name;



    public void Clear() => SelectedMacro = null;



    public void SaveSelectedMacro()

    {

        if (_selectedMacro == null)

            return;



        _selectedMacro.RunOnce = RunOnce;

        _probeMacros.Save();

    }



    public void AddOrUpdateMacro()

    {

        if (_selectedMacro != null)

        {

            _selectedMacro.RunOnce = RunOnce;

            _selectedMacro.PreCommands = PreMacroText.TrimEnd('\r', '\n');

            _selectedMacro.PostCommands = PostMacroText.TrimEnd('\r', '\n');

        }

        else

        {

            var name = string.IsNullOrWhiteSpace(NewMacroName)

                ? $"MC_{Random.Shared.Next(0, 1000)}"

                : NewMacroName.Trim();



            SelectedMacro = new ProbingMacro(name, PreMacroText, PostMacroText, RunOnce);

            Macros.Add(SelectedMacro);

            NewMacroName = string.Empty;

        }



        _probeMacros.Save();

        OnPropertyChanged(nameof(PreJobCommands));

        OnPropertyChanged(nameof(PostJobCommands));

    }



    public void DeleteSelectedMacro()

    {

        if (_selectedMacro is not { Id: > 0 })

            return;



        var found = Macros.FirstOrDefault(x => x.Id == _selectedMacro.Id);

        if (found != null)

            Macros.Remove(found);



        _probeMacros.Save();

        SelectedMacro = Macros.FirstOrDefault();

    }



    /// <summary>Prompt to save editor changes; returns true if closed without pending edits.</summary>

    public bool TryCloseEditor()

    {

        if (_selectedMacro == null)

            return true;



        if (_selectedMacro.RunOnce == RunOnce &&

            _selectedMacro.PreCommands == PreMacroText &&

            _selectedMacro.PostCommands == PostMacroText)

            return true;



        if (!GrblUi.AskYesNo(ProbingStrings.MacroChangedSave, "ioSender"))

        {

            RunOnce = _selectedMacro.RunOnce;

            PreMacroText = _selectedMacro.PreCommands;

            PostMacroText = _selectedMacro.PostCommands;

            return true;

        }



        AddOrUpdateMacro();

        return true;

    }



    public void SelectMacroById(int id)

    {

        SelectedMacro = Macros.FirstOrDefault(m => m.Id == id) ?? Macros.FirstOrDefault();

    }



    static string[] SplitCommands(string commands) =>

        string.IsNullOrWhiteSpace(commands)

            ? NoCommands

            : commands.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);



    sealed class ProbingMacros

    {

        public ObservableCollection<ProbingMacro> Macros { get; private set; } = [];



        public void Save()

        {

            var path = Core.Resources.ConfigPath + MacrosFileName;

            var xs = new XmlSerializer(typeof(ObservableCollection<ProbingMacro>));

            try

            {

                using var fs = File.Create(path);

                xs.Serialize(fs, Macros);

            }

            catch (Exception e)

            {

                GrblUi.ShowError(e.Message, "ioSender");

            }

        }



        public void Load()

        {

            var path = Core.Resources.ConfigPath + MacrosFileName;

            var xs = new XmlSerializer(typeof(ObservableCollection<ProbingMacro>));



            try

            {

                using var reader = new StreamReader(path);

                Macros = (ObservableCollection<ProbingMacro>)xs.Deserialize(reader)!;

            }

            catch

            {

                Macros = [];

            }



            foreach (var macro in Macros)

            {

                if (macro.Id == 0 && macro.Name != "<no action>")

                    macro.Id = ++ProbingMacro.NextId;

            }



            var noAction = false;

            foreach (var macro in Macros)

            {

                if (macro.Id == 0)

                    noAction = true;

                ProbingMacro.NextId = Math.Max(ProbingMacro.NextId, macro.Id);

            }



            if (!noAction)

                Macros.Insert(0, new ProbingMacro("<no action>", string.Empty, string.Empty, false, 0));

        }

    }

}



[Serializable]

public class ProbingMacro

{

    public static int NextId;



    public ProbingMacro()

    {

    }



    public ProbingMacro(string name, string preCommand, string postCommand, bool runOnce, int id = -1)

    {

        Id = id == -1 ? ++NextId : id;

        Name = name;

        PreCommands = preCommand;

        PostCommands = postCommand;

        RunOnce = runOnce;

    }



    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PreCommands { get; set; } = string.Empty;

    public string PostCommands { get; set; } = string.Empty;

    public bool RunOnce { get; set; }

}


