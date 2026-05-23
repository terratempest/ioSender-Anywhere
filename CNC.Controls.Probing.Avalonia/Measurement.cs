using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public sealed class Measurement : ViewModelBase
{
    public void Add(Position position, AxisFlags axisFlags, ProbingType probingType)
    {
        Position = position;
        AxisFlags = axisFlags;
        ProbingType = probingType;
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(AxisFlags));
        OnPropertyChanged(nameof(ProbingType));
    }

    public Position Position { get; private set; } = new();

    public ProbingType ProbingType { get; private set; } = ProbingType.None;

    public AxisFlags AxisFlags { get; private set; } = AxisFlags.None;
}
