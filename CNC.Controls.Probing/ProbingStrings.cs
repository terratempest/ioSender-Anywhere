namespace CNC.Controls.Probing;

internal static class ProbingStrings
{
    public const string NoProbe = "Probe is not connected!";
    public const string ProbeAsserted = "Probe is asserted!";
    public const string FailedNotIdle = "Machine is not idle!";
    public const string FailedNoPos = "Machine position is not known!";
    public const string Probing = "Probing...";
    public const string FailedAlarm = "Probing failed: alarm active!";
    public const string FailedCancelled = "Probing failed or was cancelled!";
    public const string ProbingAtX0Y0 = "Probing at X0 Y0";
    public const string NoVerifyContinue = "Probe is not asserted. Continue anyway?";
    public const string VerifyStart = "Probe verified - ready to start.";
    public const string ErrorProbingDistance = "XY clearance + probe radius must be less than probe distance!";
    public const string ErrorLatchDistance = "Latch distance must be less than probe distance!";
    public const string IllegalPosition = "Illegal probe position, try again.";
    public const string HeightMapNarrow = "Height map can't be infinitely narrow.";
    public const string HeightMapMinSize = "Height map must have at least 4 points.";
    public const string MacroChangedSave = "Macro was changed, save?";
    public const string SelectEdgeType = "Select edge or corner to probe.";
    public const string SelectCenterType = "Select inside or outside center probing.";
    public const string OffsetWarning = "XY clearance is greater than offset.\nContinue with offset as clearance?";
    public const string PositionUnknown = "Probing failed, machine position not known.";
    public const string ProbingCompleted = "Probing completed.";
    public const string ProbingFailed = "Probing failed";
    public const string ProbedAngle = "Probed angle: {0} deg";
    public const string AreaOriginConfirm = "Move to height map origin X{0} Y{1}?";
    public const string ProbingPointOf = "Probing point {0} of {1}";
    public const string CenterCompleted = "Probing completed. Size X={0} Y={1}";
    public const string HeightMapCompleted = "Height map completed. Z min={0} max={1}";
    public const string ProbingPass = "Pass {0} of {1}";
    public const string WorkpieceSizeRequired = "Workpiece {0} size cannot be 0.";
    public const string ClearanceTooLarge = "Workpiece {0} is too small for clearance + probe diameter.";
    public const string InitFailed = "Init failed:";
    public const string NoFileForTransform = "Load a G-code file before applying a transform.";
    public const string HasG17G18Arcs =
        "G-code contains arcs in XZ or YZ plane (G18/19); can't apply transform. Use Arcs to Lines if needed.";
    public const string HasRadiusArcs =
        "G-code contains radius-mode arcs; can't apply transform. Use Arcs to Lines if needed.";
}
