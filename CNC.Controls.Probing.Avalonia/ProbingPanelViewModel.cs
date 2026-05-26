using System.Collections.ObjectModel;
using System.ComponentModel;
using CNC.Core;

namespace CNC.Controls.Probing;

public class ProbingPanelViewModel : ViewModelBase
{
    readonly ProbingProfiles _profiles = new();
    ProbingProfile? _profile;
    GrblViewModel? _grbl;
    string _position = string.Empty;
    string _instructions = "Select a probing tab.";

    double _probeDiameter = 2d;
    double _touchPlateHeight = 1d;
    double _fixtureHeight = 1d;
    double _rapidsFeedRate;
    double _probeFeedRate = 100d;
    double _latchFeedRate = 25d;
    double _probeDistance = 10d;
    double _latchDistance = .5d;
    double _xyClearance = 5d;
    double _offset = 5d;
    double _depth = 3d;
    double _probeOffsetX;
    double _probeOffsetY;
    bool _touchPlateIsXY;

    public ObservableCollection<ProbingProfile> Profiles => _profiles.Profiles;

    public ProbingProfiles ProfileStore => _profiles;

    public GrblViewModel? Grbl => _grbl;

    public void Attach(GrblViewModel grbl)
    {
        if (_grbl == grbl)
            return;

        if (_grbl != null)
            _grbl.PropertyChanged -= OnGrblPropertyChanged;

        _grbl = grbl;
        _profiles.Load();
        OnPropertyChanged(nameof(Grbl));
        OnPropertyChanged(nameof(Profiles));
        Profile = Profiles.FirstOrDefault();
        _grbl.PropertyChanged += OnGrblPropertyChanged;
        UpdatePosition();
    }

    public void Detach()
    {
        if (_grbl != null)
            _grbl.PropertyChanged -= OnGrblPropertyChanged;
        _grbl = null;
        OnPropertyChanged(nameof(Grbl));
    }

    public ProbingProfile? Profile
    {
        get => _profile;
        set
        {
            if (_profile == value)
                return;
            _profile = value;
            ApplyFromProfile();
            OnPropertyChanged();
        }
    }

    public string Position
    {
        get => _position;
        private set { _position = value; OnPropertyChanged(); }
    }

    public string Instructions
    {
        get => _instructions;
        set { _instructions = value; OnPropertyChanged(); }
    }

    public double ProbeDiameter
    {
        get => _probeDiameter;
        set { _probeDiameter = value; OnPropertyChanged(); }
    }

    public double TouchPlateHeight
    {
        get => _touchPlateHeight;
        set { _touchPlateHeight = value; OnPropertyChanged(); }
    }

    public double FixtureHeight
    {
        get => _fixtureHeight;
        set { _fixtureHeight = value; OnPropertyChanged(); }
    }

    public double RapidsFeedRate
    {
        get => _rapidsFeedRate;
        set { _rapidsFeedRate = value; OnPropertyChanged(); }
    }

    public double ProbeFeedRate
    {
        get => _probeFeedRate;
        set { _probeFeedRate = value; OnPropertyChanged(); }
    }

    public double LatchFeedRate
    {
        get => _latchFeedRate;
        set { _latchFeedRate = value; OnPropertyChanged(); }
    }

    public double ProbeDistance
    {
        get => _probeDistance;
        set { _probeDistance = value; OnPropertyChanged(); }
    }

    public double LatchDistance
    {
        get => _latchDistance;
        set { _latchDistance = value; OnPropertyChanged(); }
    }

    public double XYClearance
    {
        get => _xyClearance;
        set { _xyClearance = value; OnPropertyChanged(); }
    }

    public double Offset
    {
        get => _offset;
        set { _offset = value; OnPropertyChanged(); }
    }

    public double Depth
    {
        get => _depth;
        set { _depth = value; OnPropertyChanged(); }
    }

    public double ProbeOffsetX
    {
        get => _probeOffsetX;
        set { _probeOffsetX = value; OnPropertyChanged(); }
    }

    public double ProbeOffsetY
    {
        get => _probeOffsetY;
        set { _probeOffsetY = value; OnPropertyChanged(); }
    }

    public bool TouchPlateIsXY
    {
        get => _touchPlateIsXY;
        set { _touchPlateIsXY = value; OnPropertyChanged(); }
    }

    public void ApplyFromProfile()
    {
        if (_profile == null)
            return;

        ProbeDiameter = _profile.ProbeDiameter;
        TouchPlateHeight = _profile.TouchPlateHeight;
        FixtureHeight = _profile.FixtureHeight;
        RapidsFeedRate = _profile.RapidsFeedRate;
        ProbeFeedRate = _profile.ProbeFeedRate;
        LatchFeedRate = _profile.LatchFeedRate;
        ProbeDistance = _profile.ProbeDistance;
        LatchDistance = _profile.LatchDistance;
        XYClearance = _profile.XYClearance;
        Offset = _profile.Offset;
        Depth = _profile.Depth;
        ProbeOffsetX = _profile.ProbeOffsetX;
        ProbeOffsetY = _profile.ProbeOffsetY;
        TouchPlateIsXY = _profile.TouchPlateIsXY;
    }

    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.Position) or nameof(GrblViewModel.Signals))
            UpdatePosition();
    }

    void UpdatePosition()
    {
        if (_grbl == null)
        {
            Position = string.Empty;
            return;
        }

        var position = new Position(_grbl.Position, _grbl.UnitFactor);
        var probe = _grbl.Signals.Value.HasFlag(Signals.Probe) ? " P" : string.Empty;
        var disconnected = _grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected) ? " D" : string.Empty;
        Position = string.Format(
            "X:{0}  Y:{1}  Z:{2}{3}{4}",
            position.X.ToInvariantString(_grbl.Format),
            position.Y.ToInvariantString(_grbl.Format),
            position.Z.ToInvariantString(_grbl.Format),
            probe,
            disconnected);
    }
}
