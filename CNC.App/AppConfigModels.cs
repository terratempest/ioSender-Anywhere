using System.Collections.ObjectModel;
using System.Xml.Serialization;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.App.Workspace;
using CNC.Platform.Abstractions;
using static CNC.GCode.GCodeParser;

namespace CNC.App;

[Serializable]
public class ThemeColorSetting : ViewModelBase
{
    private string _key = string.Empty;
    private string _value = "#FFFFFFFF";

    public string Key
    {
        get => _key;
        set { _key = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string Value
    {
        get => _value;
        set { _value = value ?? string.Empty; OnPropertyChanged(); }
    }

    public ThemeColorSetting Clone() => new() { Key = Key, Value = Value };
}

[Serializable]
public class AppThemeDefinition : ViewModelBase
{
    private string _name = string.Empty;
    private string _baseTheme = AppThemeKeys.Dark;
    private bool _useSystemAccentColor = true;
    private bool _useSystemAccentColorSpecified;
    private ObservableCollection<ThemeColorSetting> _colors = new();

    public string Name
    {
        get => _name;
        set { _name = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string BaseTheme
    {
        get => _baseTheme;
        set { _baseTheme = AppThemeKeys.Normalize(value); OnPropertyChanged(); }
    }

    public bool UseSystemAccentColor
    {
        get => _useSystemAccentColor;
        set
        {
            _useSystemAccentColor = value;
            _useSystemAccentColorSpecified = true;
            OnPropertyChanged();
        }
    }

    [XmlIgnore]
    public bool UseSystemAccentColorSpecified
    {
        get => _useSystemAccentColorSpecified;
        set => _useSystemAccentColorSpecified = value;
    }

    public ObservableCollection<ThemeColorSetting> Colors
    {
        get => _colors;
        set { _colors = value ?? new ObservableCollection<ThemeColorSetting>(); OnPropertyChanged(); }
    }

    public AppThemeDefinition Clone() => new()
    {
        Name = Name,
        BaseTheme = BaseTheme,
        UseSystemAccentColor = UseSystemAccentColor,
        Colors = new ObservableCollection<ThemeColorSetting>(Colors.Select(c => c.Clone())),
    };
}

[Serializable]
public class LatheConfig : ViewModelBase
{
    private bool _isEnabled;
    private LatheMode _latheMode = LatheMode.Disabled;

    [XmlIgnore]
    public double ZDirFactor => ZDirection == Direction.Negative ? -1d : 1d;

    [XmlIgnore]
    public LatheMode[] LatheModes => (LatheMode[])Enum.GetValues(typeof(LatheMode));

    [XmlIgnore]
    public Direction[] ZDirections => (Direction[])Enum.GetValues(typeof(Direction));

    [XmlIgnore]
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public LatheMode XMode
    {
        get => _latheMode;
        set { _latheMode = value; IsEnabled = value != LatheMode.Disabled; }
    }

    public Direction ZDirection { get; set; } = Direction.Negative;
    public double PassDepthLast { get; set; } = 0.02d;
    public double FeedRate { get; set; } = 300d;
}

[Serializable]
public class ProbeConfig : ViewModelBase
{
    private bool _checkProbeStatus = true;
    private bool _validateProbeConnected;

    public bool CheckProbeStatus
    {
        get => _checkProbeStatus;
        set { _checkProbeStatus = value; OnPropertyChanged(); }
    }

    public bool ValidateProbeConnected
    {
        get => _validateProbeConnected;
        set { _validateProbeConnected = value; OnPropertyChanged(); }
    }
}

[Serializable]
public class CameraConfig : ViewModelBase
{
    private string _camera = string.Empty;
    private int _captureWidth = 640, _captureHeight = 480;
    private double _xoffset, _yoffset, _crossHairX = -1d, _crossHairY = -1d;
    private int _guideScale = 10;
    private bool _moveToSpindle, _confirmMove;
    private CameraMoveMode _moveMode = CameraMoveMode.BothAxes;

    [XmlIgnore]
    internal bool IsDirty { get; set; }

    [XmlIgnore]
    public CameraMoveMode[] MoveModes => (CameraMoveMode[])Enum.GetValues(typeof(CameraMoveMode));

    public string SelectedCamera
    {
        get => _camera;
        set
        {
            _camera = value;
            IsDirty = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceIndex));
        }
    }

    /// <summary>OpenCV device index (persisted via <see cref="SelectedCamera"/>).</summary>
    [XmlIgnore]
    public int DeviceIndex
    {
        get => int.TryParse(_camera, out var i) ? i : 0;
        set => SelectedCamera = value.ToString();
    }

    public int CaptureWidth
    {
        get => _captureWidth;
        set { _captureWidth = value < 160 ? 160 : value; IsDirty = true; OnPropertyChanged(); }
    }

    public int CaptureHeight
    {
        get => _captureHeight;
        set { _captureHeight = value < 120 ? 120 : value; IsDirty = true; OnPropertyChanged(); }
    }

    public double XOffset
    {
        get => _xoffset;
        set { _xoffset = value; OnPropertyChanged(); }
    }

    public double YOffset
    {
        get => _yoffset;
        set { _yoffset = value; OnPropertyChanged(); }
    }

    public double CrosshairPosX
    {
        get => _crossHairX;
        set { _crossHairX = value; IsDirty = true; OnPropertyChanged(); }
    }

    public double CrosshairPosY
    {
        get => _crossHairY;
        set { _crossHairY = value; IsDirty = true; OnPropertyChanged(); }
    }

    public int GuideScale
    {
        get => _guideScale;
        set { _guideScale = value; IsDirty = true; OnPropertyChanged(); }
    }

    public bool InitialMoveToSpindle
    {
        get => _moveToSpindle;
        set { _moveToSpindle = value; IsDirty = true; OnPropertyChanged(); }
    }

    public bool ConfirmMove
    {
        get => _confirmMove;
        set { _confirmMove = value; IsDirty = true; OnPropertyChanged(); }
    }

    public CameraMoveMode MoveMode
    {
        get => _moveMode;
        set { _moveMode = value; OnPropertyChanged(); }
    }
}

[Serializable]
public class GCodeViewerConfig : ViewModelBase
{
    private bool _isEnabled = true;
    private int _arcResolution = 10;
    private double _minDistance = 0.05d, _toolDiameter = 3d;
    private bool _showGrid = true, _showAxes = true, _showBoundingBox, _showViewCube = true, _showCoordSystem, _showWorkEnvelope;
    private bool _showTextOverlay, _renderExecuted, _blackBackground = true, _scaleTool = true;
    private UiColor _cutMotion = UiColor.Red, _rapidMotion = UiColor.LightPink, _retractMotion = UiColor.Green;
    private UiColor _toolOrigin = UiColor.Green, _grid = UiColor.Gray, _highlight = UiColor.Crimson;

    [XmlIgnore]
    public bool IsHomingEnabled
    {
        get => _isEnabled && GrblInfo.HomingEnabled;
        set => OnPropertyChanged();
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsHomingEnabled)); }
    }

    public int ArcResolution
    {
        get => _arcResolution;
        set { _arcResolution = value; OnPropertyChanged(); }
    }

    public double MinDistance
    {
        get => _minDistance;
        set { _minDistance = value; OnPropertyChanged(); }
    }

    public bool ToolAutoScale
    {
        get => _scaleTool;
        set { _scaleTool = value; OnPropertyChanged(); }
    }

    public double ToolDiameter
    {
        get => _toolDiameter;
        set { _toolDiameter = value; OnPropertyChanged(); }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set { _showGrid = value; OnPropertyChanged(); }
    }

    public bool ShowAxes
    {
        get => _showAxes;
        set { _showAxes = value; OnPropertyChanged(); }
    }

    public bool ShowBoundingBox
    {
        get => _showBoundingBox;
        set { _showBoundingBox = value; OnPropertyChanged(); }
    }

    public bool ShowWorkEnvelope
    {
        get => _showWorkEnvelope && GrblInfo.HomingEnabled;
        set { _showWorkEnvelope = value; OnPropertyChanged(); }
    }

    public bool ShowViewCube
    {
        get => _showViewCube;
        set { _showViewCube = value; OnPropertyChanged(); }
    }

    public bool ShowTextOverlay
    {
        get => _showTextOverlay;
        set { _showTextOverlay = value; OnPropertyChanged(); }
    }

    public bool ShowCoordinateSystem
    {
        get => _showCoordSystem;
        set { _showCoordSystem = value; OnPropertyChanged(); }
    }

    public bool RenderExecuted
    {
        get => _renderExecuted;
        set { _renderExecuted = value; OnPropertyChanged(); }
    }

    public bool BlackBackground
    {
        get => _blackBackground;
        set { _blackBackground = value; OnPropertyChanged(); }
    }

    public UiColor CutMotionColor
    {
        get => _cutMotion;
        set { _cutMotion = value; OnPropertyChanged(); }
    }

    public UiColor RapidMotionColor
    {
        get => _rapidMotion;
        set { _rapidMotion = value; OnPropertyChanged(); }
    }

    public UiColor RetractMotionColor
    {
        get => _retractMotion;
        set { _retractMotion = value; OnPropertyChanged(); }
    }

    public UiColor ToolOriginColor
    {
        get => _toolOrigin;
        set { _toolOrigin = value; OnPropertyChanged(); }
    }

    public UiColor GridColor
    {
        get => _grid;
        set { _grid = value; OnPropertyChanged(); }
    }

    public UiColor HighlightColor
    {
        get => _highlight;
        set { _highlight = value; OnPropertyChanged(); }
    }

    public int ViewMode { get; set; } = -1;
    public int ToolVisualizer { get; set; } = 1;
    public Point3D CameraPosition { get; set; }
    public Vector3 CameraLookDirection { get; set; }
    public Vector3 CameraUpDirection { get; set; }
}

[Serializable]
public class JogUIConfig : ViewModelBase
{
    private readonly int[] _feedrate = new int[4];
    private readonly double[] _distance = new double[4];

    public JogUIConfig()
    {
    }

    public JogUIConfig(int[] feedrate, double[] distance)
    {
        for (var i = 0; i < feedrate.Length && i < _feedrate.Length; i++)
        {
            _feedrate[i] = feedrate[i];
            _distance[i] = distance[i];
        }
    }

    [XmlIgnore]
    public int[] Feedrate => _feedrate;

    public int Feedrate0 { get => _feedrate[0]; set { _feedrate[0] = value; OnPropertyChanged(); } }
    public int Feedrate1 { get => _feedrate[1]; set { _feedrate[1] = value; OnPropertyChanged(); } }
    public int Feedrate2 { get => _feedrate[2]; set { _feedrate[2] = value; OnPropertyChanged(); } }
    public int Feedrate3 { get => _feedrate[3]; set { _feedrate[3] = value; OnPropertyChanged(); } }

    [XmlIgnore]
    public double[] Distance => _distance;

    public double Distance0 { get => _distance[0]; set { _distance[0] = value; OnPropertyChanged(); } }
    public double Distance1 { get => _distance[1]; set { _distance[1] = value; OnPropertyChanged(); } }
    public double Distance2 { get => _distance[2]; set { _distance[2] = value; OnPropertyChanged(); } }
    public double Distance3 { get => _distance[3]; set { _distance[3] = value; OnPropertyChanged(); } }
}

[Serializable]
public class JogConfig : ViewModelBase
{
    public enum JogMode : int
    {
        UI = 0,
        Keypad,
        KeypadAndUI
    }

    private bool _kbEnable, _linkStepToUi = true;
    private JogMode _jogMode = JogMode.UI;
    private double _fastFeedrate = 500d, _slowFeedrate = 200d, _stepFeedrate = 100d;
    private double _fastDistance = 500d, _slowDistance = 500d, _stepDistance = 0.05d;

    public JogMode Mode
    {
        get => _jogMode;
        set { _jogMode = value; OnPropertyChanged(); }
    }

    public bool KeyboardEnable
    {
        get => _kbEnable;
        set { _kbEnable = value; OnPropertyChanged(); }
    }

    public bool LinkStepJogToUI
    {
        get => _linkStepToUi;
        set { _linkStepToUi = value; OnPropertyChanged(); }
    }

    public double FastFeedrate
    {
        get => _fastFeedrate;
        set { _fastFeedrate = value; OnPropertyChanged(); }
    }

    public double SlowFeedrate
    {
        get => _slowFeedrate;
        set { _slowFeedrate = value; OnPropertyChanged(); }
    }

    public double StepFeedrate
    {
        get => _stepFeedrate;
        set { _stepFeedrate = value; OnPropertyChanged(); }
    }

    public double FastDistance
    {
        get => _fastDistance;
        set { _fastDistance = value; OnPropertyChanged(); }
    }

    public double SlowDistance
    {
        get => _slowDistance;
        set { _slowDistance = value; OnPropertyChanged(); }
    }

    public double StepDistance
    {
        get => _stepDistance;
        set { _stepDistance = value; OnPropertyChanged(); }
    }
}

[Serializable]
public enum GameControllerAction
{
    JogYPlus,
    JogYMinus,
    JogXMinus,
    JogXPlus,
    JogZPlus,
    JogZMinus,
    CycleJogDistance,
    CycleJogFeedRate,
    JogDistanceMode,
    JogCancel
}

[Serializable]
public enum GameControllerInputKind
{
    Button,
    Axis
}

[Serializable]
public class GameControllerBinding : ViewModelBase
{
    private GameControllerAction _action;
    private GameControllerInputKind _inputKind;
    private string _inputName = string.Empty;
    private int _threshold = 16000;

    public GameControllerAction Action
    {
        get => _action;
        set { _action = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActionLabel)); }
    }

    public GameControllerInputKind InputKind
    {
        get => _inputKind;
        set { _inputKind = value; OnPropertyChanged(); OnPropertyChanged(nameof(InputDisplay)); }
    }

    public string InputName
    {
        get => _inputName;
        set { _inputName = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(InputDisplay)); }
    }

    public int Threshold
    {
        get => _threshold;
        set { _threshold = value; OnPropertyChanged(); }
    }

    [XmlIgnore]
    public string ActionLabel => GameControllerConfig.GetActionLabel(Action);

    [XmlIgnore]
    public string InputDisplay => InputKind == GameControllerInputKind.Axis
        ? $"{InputName} axis"
        : InputName;

    public GameControllerBinding Clone() => new()
    {
        Action = Action,
        InputKind = InputKind,
        InputName = InputName,
        Threshold = Threshold
    };
}

[Serializable]
public class GameControllerConfig : ViewModelBase
{
    private bool _enabled;
    private string _preferredControllerName = string.Empty;
    private ObservableCollection<GameControllerBinding> _bindings = CreateDefaultBindings();

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; OnPropertyChanged(); }
    }

    public string PreferredControllerName
    {
        get => _preferredControllerName;
        set { _preferredControllerName = value ?? string.Empty; OnPropertyChanged(); }
    }

    public ObservableCollection<GameControllerBinding> Bindings
    {
        get => _bindings;
        set
        {
            _bindings = value ?? CreateDefaultBindings();
            EnsureDefaultBindings();
            OnPropertyChanged();
        }
    }

    public void ResetBindings()
    {
        Bindings = CreateDefaultBindings();
    }

    public void EnsureDefaultBindings()
    {
        DeduplicateBindings();
        var defaults = CreateDefaultBindings();

        foreach (var defaultBinding in defaults)
        {
            if (_bindings.Any(binding => binding.Action == defaultBinding.Action))
                continue;

            _bindings.Add(defaultBinding);
        }
    }

    void DeduplicateBindings()
    {
        var seen = new HashSet<GameControllerAction>();
        for (var i = _bindings.Count - 1; i >= 0; i--)
        {
            if (seen.Add(_bindings[i].Action))
                continue;

            _bindings.RemoveAt(i);
        }
    }

    public static ObservableCollection<GameControllerBinding> CreateDefaultBindings() =>
    [
        Button(GameControllerAction.JogYPlus, "DPadUp"),
        Button(GameControllerAction.JogYMinus, "DPadDown"),
        Button(GameControllerAction.JogXMinus, "DPadLeft"),
        Button(GameControllerAction.JogXPlus, "DPadRight"),
        Button(GameControllerAction.JogZPlus, "East"),
        Button(GameControllerAction.JogZMinus, "South"),
        Button(GameControllerAction.CycleJogDistance, "LeftShoulder"),
        Button(GameControllerAction.CycleJogFeedRate, "RightShoulder"),
        Axis(GameControllerAction.JogDistanceMode, "LeftTrigger"),
        Button(GameControllerAction.JogCancel, "Start"),
    ];

    public static string GetActionLabel(GameControllerAction action) => action switch
    {
        GameControllerAction.JogYPlus => "Jog Y+",
        GameControllerAction.JogYMinus => "Jog Y-",
        GameControllerAction.JogXMinus => "Jog X-",
        GameControllerAction.JogXPlus => "Jog X+",
        GameControllerAction.JogZPlus => "Jog Z+",
        GameControllerAction.JogZMinus => "Jog Z-",
        GameControllerAction.CycleJogDistance => "Cycle jog distance",
        GameControllerAction.CycleJogFeedRate => "Cycle jog feed rate",
        GameControllerAction.JogDistanceMode => "Distance mode while held",
        GameControllerAction.JogCancel => "Cancel jog",
        _ => action.ToString()
    };

    static GameControllerBinding Button(GameControllerAction action, string button) => new()
    {
        Action = action,
        InputKind = GameControllerInputKind.Button,
        InputName = button
    };

    static GameControllerBinding Axis(GameControllerAction action, string axis) => new()
    {
        Action = action,
        InputKind = GameControllerInputKind.Axis,
        InputName = axis,
        Threshold = 16000
    };
}

public interface IGameControllerBindingCapture
{
    string StatusText { get; }

    Task<GameControllerBinding?> CaptureAsync(CancellationToken cancellationToken = default);
}

[Serializable]
public enum PopupKeyboardTrigger
{
    Off = 0,
    OneClick = 1,
    TwoClick = 2,
}

[Serializable]
public class BaseConfig : ViewModelBase
{
    private int _pollInterval = 200;
    private int _maxBufferSize = 300;
    private bool _useBuffering, _keepMdiFocus = true, _filterOkResponse, _consoleAutoscroll = true, _saveWindowSize = true, _autoCompress, _sendComments, _addLineNumbers;
    private PopupKeyboardTrigger _popupKeyboardTrigger = PopupKeyboardTrigger.TwoClick;
    private CommandIgnoreState _ignoreM6 = CommandIgnoreState.No, _ignoreM7 = CommandIgnoreState.No, _ignoreM8 = CommandIgnoreState.No;
    private CommandIgnoreState _ignoreG61G64 = CommandIgnoreState.Strip;
    private string _theme = "Dark";
    private string _locale = string.Empty;
    private bool _useSystemAccentColor = true;
    private bool _useSystemAccentColorSpecified;
    private UiLayoutMode _layoutMode = UiLayoutMode.Compact;
    private WorkspaceNode? _workspaceRoot;
    private string _workspacePreset = string.Empty;

    [XmlIgnore]
    public Dictionary<string, string> Themes { get; } = new();

    /// <summary>Persisted Blender-style workspace split tree. When null, use <see cref="LayoutMode"/> preset once.</summary>
    public WorkspaceNode? WorkspaceRoot
    {
        get => _workspaceRoot;
        set { _workspaceRoot = value; OnPropertyChanged(); }
    }

    public string WorkspacePreset
    {
        get => _workspacePreset;
        set { _workspacePreset = value ?? string.Empty; OnPropertyChanged(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(); }
    }

    public bool UseSystemAccentColor
    {
        get => _useSystemAccentColor;
        set
        {
            _useSystemAccentColor = value;
            _useSystemAccentColorSpecified = true;
            OnPropertyChanged();
        }
    }

    [XmlIgnore]
    public bool UseSystemAccentColorSpecified
    {
        get => _useSystemAccentColorSpecified;
        set => _useSystemAccentColorSpecified = value;
    }

    /// <summary>UI culture (e.g. de-DE). Empty = en-US resources, system culture unchanged unless -locale is passed.</summary>
    public string Locale
    {
        get => _locale;
        set { _locale = value ?? string.Empty; OnPropertyChanged(); }
    }

    public UiLayoutMode LayoutMode
    {
        get => _layoutMode;
        set { _layoutMode = value; OnPropertyChanged(); }
    }

    public int PollInterval
    {
        get => _pollInterval < 100 ? 100 : _pollInterval;
        set { _pollInterval = value; OnPropertyChanged(); }
    }

    public string PortParams { get; set; } = "COMn:115200,N,8,1";
    public int ResetDelay { get; set; } = 2000;

    public bool UseBuffering
    {
        get => _useBuffering;
        set { _useBuffering = value; OnPropertyChanged(); }
    }

    public bool KeepWindowSize
    {
        get => _saveWindowSize;
        set
        {
            if (_saveWindowSize != value)
            {
                _saveWindowSize = value;
                OnPropertyChanged();
            }
        }
    }

    public double WindowWidth { get; set; } = 925;
    public double WindowHeight { get; set; } = 660;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; }
    public bool WindowFullscreen { get; set; }
    public int OutlineFeedRate { get; set; } = 500;

    public int MaxBufferSize
    {
        get => _maxBufferSize < 300 ? 300 : _maxBufferSize;
        set { _maxBufferSize = value; OnPropertyChanged(); }
    }

    public string Editor { get; set; } = "";

    public bool KeepMdiFocus
    {
        get => _keepMdiFocus;
        set { _keepMdiFocus = value; OnPropertyChanged(); }
    }

    public bool FilterOkResponse
    {
        get => _filterOkResponse;
        set { _filterOkResponse = value; OnPropertyChanged(); }
    }

    public bool ConsoleAutoscroll
    {
        get => _consoleAutoscroll;
        set { _consoleAutoscroll = value; OnPropertyChanged(); }
    }

    public bool AutoCompress
    {
        get => _autoCompress;
        set { _autoCompress = value; OnPropertyChanged(); }
    }

    public bool SendComments
    {
        get => _sendComments;
        set { _sendComments = value; OnPropertyChanged(); }
    }

    public bool AddLineNumbers
    {
        get => _addLineNumbers;
        set { _addLineNumbers = value; OnPropertyChanged(); }
    }

    public PopupKeyboardTrigger PopupKeyboardTrigger
    {
        get => _popupKeyboardTrigger;
        set { _popupKeyboardTrigger = value; OnPropertyChanged(); }
    }

    [XmlIgnore]
    public CommandIgnoreState[] CommandIgnoreStates => (CommandIgnoreState[])Enum.GetValues(typeof(CommandIgnoreState));

    public CommandIgnoreState IgnoreM6
    {
        get => _ignoreM6;
        set { _ignoreM6 = value; OnPropertyChanged(); }
    }

    public CommandIgnoreState IgnoreM7
    {
        get => _ignoreM7;
        set { _ignoreM7 = value; OnPropertyChanged(); }
    }

    public CommandIgnoreState IgnoreM8
    {
        get => _ignoreM8;
        set { _ignoreM8 = value; OnPropertyChanged(); }
    }

    public CommandIgnoreState IgnoreG61G64
    {
        get => _ignoreG61G64;
        set { _ignoreG61G64 = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Macro> Macros { get; set; } = new();
    public AppThemeDefinition CustomThemeDraft { get; set; } = new()
    {
        Name = AppThemeKeys.Custom,
        BaseTheme = AppThemeKeys.Dark,
    };

    [XmlIgnore]
    public ObservableCollection<AppThemeDefinition> UserThemes { get; set; } = new();

    [XmlArray("UserThemes")]
    public ObservableCollection<AppThemeDefinition> LegacyUserThemes { get; set; } = new();

    public bool ShouldSerializeLegacyUserThemes() => false;

    public JogConfig Jog { get; set; } = new();
    public JogUIConfig JogUiMetric { get; set; } = new(new[] { 5, 100, 500, 1000 }, new[] { .01d, .1d, 1d, 10d });
    public JogUIConfig JogUiImperial { get; set; } = new(new[] { 5, 10, 50, 100 }, new[] { .001d, .01d, .1d, 1d });
    public GameControllerConfig GameController { get; set; } = new();
    public LatheConfig Lathe { get; set; } = new();
    public CameraConfig Camera { get; set; } = new();
    public GCodeViewerConfig GCodeViewer { get; set; } = new();
    public ProbeConfig Probing { get; set; } = new();
    public QuickAccessSidebarConfig QuickAccessSidebar { get; set; } = new();
}
