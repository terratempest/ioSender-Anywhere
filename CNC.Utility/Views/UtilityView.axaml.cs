using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Controls.Avalonia.Views;
using CNC.Core;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;
using CNC.Utility.GCode;

namespace CNC.Utility.Views;

public partial class UtilityView : UserControl
{
    UtilityGCodePreview _preview = new([], []);

    public event EventHandler<UtilityProgramGeneratedEventArgs>? ProgramGenerated;

    public GCodeViewerSession? PreviewSession
    {
        get => SurfacingPreview.Session;
        set => SurfacingPreview.Session = value;
    }

    public UtilityView()
    {
        InitializeComponent();

        if (Design.IsDesignMode && SurfacingPreview.Session is null)
            SurfacingPreview.Session = CreateDesignPreviewSession();
    }

    static GCodeViewerSession CreateDesignPreviewSession() =>
        new(new AppConfigService(), new GrblViewModel(), () => [], () => []);

    void OnPreviewSurfacingClick(object? sender, RoutedEventArgs e) => PreviewSurfacing("Utility surfacing preview");

    void OnSendSurfacingClick(object? sender, RoutedEventArgs e) => SendSurfacing("Utility surfacing");

    void OnPreviewDrillingClick(object? sender, RoutedEventArgs e) => PreviewDrilling("Utility drilling preview");

    void OnSendDrillingClick(object? sender, RoutedEventArgs e) => SendDrilling("Utility drilling");

    void OnPreviewSelectedUtilityClick(object? sender, RoutedEventArgs e)
    {
        if (UtilityTabs.SelectedIndex == 1)
            PreviewDrilling("Utility drilling preview");
        else
            PreviewSurfacing("Utility surfacing preview");
    }

    void OnSendSelectedUtilityClick(object? sender, RoutedEventArgs e)
    {
        if (UtilityTabs.SelectedIndex == 1)
            SendDrilling("Utility drilling");
        else
            SendSurfacing("Utility surfacing");
    }

    public IReadOnlyList<GCodeToken> PreviewTokens => _preview.Tokens;

    public IReadOnlyList<GCodeBlock> PreviewBlocks => _preview.Blocks;

    public void ClosePreview() => SurfacingPreview.Close();

    void PreviewSurfacing(string displayName)
    {
        RunGeneratedPreview(BuildSurfacingLines(), displayName);
    }

    void SendSurfacing(string displayName)
    {
        SendGeneratedProgram(BuildSurfacingLines(), displayName);
    }

    void PreviewDrilling(string displayName)
    {
        RunGeneratedPreview(BuildDrillingLines(), displayName);
    }

    void SendDrilling(string displayName)
    {
        SendGeneratedProgram(BuildDrillingLines(), displayName);
    }

    IReadOnlyList<string> BuildSurfacingLines()
    {
        try
        {
            return UtilityGCodeGenerator.GenerateSurfacing(new SurfacingOptions(
                GetUnits(),
                SurfacingOrigin.SelectedIndex == 1 ? UtilityOrigin.Center : UtilityOrigin.LowerLeft,
                ReadDouble(SurfacingOriginX, "Origin X"),
                ReadDouble(SurfacingOriginY, "Origin Y"),
                ReadDouble(SurfacingOriginZ, "Origin Z"),
                ReadDouble(SurfacingToolDiameter, "Tool diameter"),
                ReadDouble(SurfacingLengthX, "Material X length"),
                ReadDouble(SurfacingWidthY, "Material Y width"),
                ReadDouble(SurfacingDepth, "Target depth"),
                ReadInt(SurfacingStepDownPasses, "Step-down passes"),
                ReadInt(SurfacingFinishPasses, "Finish repeats"),
                ReadDouble(SurfacingToolEngagement, "Tool engagement"),
                ReadDouble(SurfacingFeed, "Feed rate"),
                ReadDouble(SurfacingPlungeFeed, "Plunge feed"),
                ReadDouble(SurfacingSafeZ, "Safe Z"),
                ReadInt(SurfacingRpm, "Spindle RPM"),
                new CoolantOptions(SurfacingFlood.IsChecked == true, SurfacingMist.IsChecked == true),
                SurfacingDirection.SelectedIndex == 1 ? SurfacingPassDirection.AlongY : SurfacingPassDirection.AlongX,
                SurfacingCutType.SelectedIndex switch
                {
                    1 => CNC.Utility.GCode.SurfacingCutType.Climb,
                    2 => CNC.Utility.GCode.SurfacingCutType.Conventional,
                    _ => CNC.Utility.GCode.SurfacingCutType.Both,
                }));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            UtilityStatus.Text = ex.Message;
            GrblUi.ShowError(ex.Message, "Utility");
            return [];
        }
    }

    IReadOnlyList<string> BuildDrillingLines()
    {
        try
        {
            return UtilityGCodeGenerator.GenerateDrilling(new DrillingOptions(
                GetUnits(),
                ReadDouble(DrillSafeZ, "Safe Z"),
                ReadDouble(DrillRetractZ, "Retract Z"),
                ReadDouble(DrillFeed, "Feed rate"),
                ReadDouble(DrillPlungeFeed, "Plunge feed"),
                ReadInt(DrillRpm, "Spindle RPM"),
                new CoolantOptions(DrillFlood.IsChecked == true, DrillMist.IsChecked == true),
                [new DrillHole(
                    ReadDouble(DrillX, "Hole X"),
                    ReadDouble(DrillY, "Hole Y"),
                    ReadDouble(DrillDepth, "Depth"),
                    ReadDouble(DrillDwell, "Dwell seconds"))]));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            UtilityStatus.Text = ex.Message;
            GrblUi.ShowError(ex.Message, "Utility");
            return [];
        }
    }

    void RunGeneratedPreview(IReadOnlyList<string> lines, string displayName)
    {
        if (lines.Count == 0)
            return;

        try
        {
            _preview = UtilityGCodePreviewParser.Parse(lines, displayName);
            SurfacingPreview.Open(_preview.Tokens);
            UtilityStatus.Text = $"{lines.Count} lines previewed";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            UtilityStatus.Text = ex.Message;
            GrblUi.ShowError(ex.Message, "Utility");
        }
    }

    void SendGeneratedProgram(IReadOnlyList<string> lines, string displayName)
    {
        if (lines.Count == 0)
            return;

        ProgramGenerated?.Invoke(this, new UtilityProgramGeneratedEventArgs(lines, displayName));
        UtilityStatus.Text = $"{lines.Count} lines loaded";
    }

    UtilityUnits GetUnits()
    {
        if (DataContext is GrblViewModel model && !model.IsMetric)
            return UtilityUnits.Imperial;

        return UtilityUnits.Metric;
    }

    static double ReadDouble(NumericField field, string label)
    {
        var value = field.Value;
        if (double.IsFinite(value))
            return value;

        throw new FormatException($"{label} must be numeric.");
    }

    static int ReadInt(NumericField field, string label)
    {
        var value = field.Value;
        if (double.IsFinite(value) && Math.Abs(value - Math.Round(value)) < 0.0000001d)
            return (int)Math.Round(value);

        throw new FormatException($"{label} must be an integer.");
    }
}

public sealed class UtilityProgramGeneratedEventArgs : EventArgs
{
    public UtilityProgramGeneratedEventArgs(IReadOnlyList<string> lines, string displayName)
    {
        Lines = lines;
        DisplayName = displayName;
    }

    public IReadOnlyList<string> Lines { get; }

    public string DisplayName { get; }
}
