namespace CNC.Controls.Config;

public enum GrblConfigType
{
    None = 0,
    Base,
    StepperCalibration,
    Trinamic,
    PidTuning
}

public interface IGrblConfigTab
{
    GrblConfigType GrblConfigType { get; }
    void Activate(bool activate);
}
