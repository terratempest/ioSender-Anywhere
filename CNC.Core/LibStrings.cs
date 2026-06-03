namespace CNC.Core;

public static class LibStrings
{
    private static readonly Dictionary<string, string> Resources = new(StringComparer.Ordinal)
    {
        ["LoadError"] = "{0}\nLine: {1}\nBlock: {2}\n\nContinue loading?",
        ["ParserStrip"] = "{0} command found, strip?",
        ["ParserStripHdr"] = "Strip command",
        ["ParserBadExpr"] = "Bad expression",
        ["ParserUnsupportedCmd"] = "Unsupported command",
        ["ParserAxisError"] = "Axis command conflict",
        ["ParserModalGrpError"] = "Modal group violation",
        ["ParserCmdUnknown"] = "Command word not recognized",
        ["ParserCMDInvalid"] = "Invalid GCode",
        ["ParserWordRepeated"] = "Command word repeated",
        ["ParserToolProfile"] = "Tool {0} not associated with a profile",
        ["ParserM66PandE"] = "Cannot use both P- and E-word with M66",
        ["ParserM66NoPorE"] = "P- or E-word missing for M66",
        ["ParserM66BadParams"] = "Illegal M66 parameters",
        ["ParserG6NoP"] = "G4 - missing P word",
        ["ParserNoG0orG1"] = "G0 or G1 not active",
        ["ParserG1NoFeed"] = "G1 used when feed rate is not set",
        ["ParserPlaneNotXY"] = "Plane not XY",
        ["ParserNoPandorQ"] = "P and/or Q word missing",
        ["ParserNoIandorJ"] = "I and/or J word missing",
        ["ParserNoR"] = "R word missing",
        ["ParserPlaneNotZX"] = "Plane not ZX",
        ["ParserZPlus"] = "Axisword(s) other than Z found",
        ["ParserNoP"] = "P word missing",
        ["ParserNegP"] = "P word negative",
        ["ParserNoI"] = "I word missing",
        ["ParserNoJ"] = "J word missing",
        ["ParserNegJ"] = "J word negative",
        ["ParserNoK"] = "K word missing",
        ["ParserNegK"] = "K word negative",
        ["ParserKlesseqJ"] = "K word must be greater than J word",
        ["ParseRless1"] = "R word less than 1",
        ["ParserNegH"] = "H word is negative",
        ["ParserErrE"] = "E word greater than half the drive line length",
        ["ParserNoInvalidQ"] = "Q word missing or out of range",
        ["ParserRadiusErr"] = "Error computing arc radius.",
        ["SerialPortError"] = "Unable to open serial port: {0}",
        ["JoggingOnly"] = "Only jogging and some system commands are allowed when changing tool!",
        ["SdStreamComplete"] = "SD Card streaming {0}% complete",
        ["ContUnlock"] = "<Unlock> to continue",
        ["ContHomeUnlock"] = "<Home> or <Unlock> to continue",
        ["ContClearResetUnlock"] = "clear then <Reset> then <Unlock> to continue",
        ["ContHome"] = "<Home> to continue",
        ["ContResetUnlock"] = "<Reset> then <Unlock> to continue",
        ["MsgHome"] = "Homing cycle required, <Home> to continue",
        ["ProbePrimary"] = "Primary",
        ["ProbeToolSetter"] = "Toolsetter",
        ["ProbeSecondary"] = "Secondary",
    };

    public static string FindResource(string key) =>
        Resources.TryGetValue(key, out var value) ? value : string.Empty;
}
