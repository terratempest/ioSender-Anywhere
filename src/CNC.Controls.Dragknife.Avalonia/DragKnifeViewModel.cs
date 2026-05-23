using Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using Action = CNC.Core.Action;

namespace CNC.Controls.DragKnife;

public class DragKnifeViewModel : ViewModelBase, IGCodeTransformer
{
    double _knifeTipOffset = 1.5d, _cutDepth = -1.00d, _swivelAngle = 20d, _dentLength = .02d, _retractAngle = 40d, _retractDepth = -0.05d;
    bool _retractEnable;

    public double KnifeTipOffset { get => _knifeTipOffset; set { _knifeTipOffset = value; OnPropertyChanged(); } }
    public double CutDepth { get => _cutDepth; set { _cutDepth = value; OnPropertyChanged(); } }
    public double SwivelAngle { get => _swivelAngle; set { _swivelAngle = value; OnPropertyChanged(); } }
    public double DentLength { get => _dentLength; set { _dentLength = value; OnPropertyChanged(); } }
    public double RetractAngle { get => _retractAngle; set { _retractAngle = value; OnPropertyChanged(); } }
    public double RetractDepth { get => _retractDepth; set { _retractDepth = value; OnPropertyChanged(); } }
    public bool RetractEnable { get => _retractEnable; set { _retractEnable = value; OnPropertyChanged(); } }

    Vector3 StartDirection = new(1d, 0d, 0d);

    sealed class Segment
    {
        public Vector3 P1;
        public Vector3 P2;
    }

    public void Apply(Window? owner)
    {
        if (!DragKnifeDialog.Show(this, owner))
            return;

        var file = GCodeFileService.Instance;
        var emu = new GCodeEmulator();
        var polyLine = new List<Segment>();
        var newToolPath = new List<GCodeToken>();

        newToolPath.Add(new GCComment(Commands.Comment, 0, "Drag knife transform applied"));
        StartDirection = new Vector3(1d, 0d, 0d);

        foreach (var cmd in emu.Execute(file.Tokens))
        {
            switch (cmd.Token.Command)
            {
                case Commands.G0:
                    if (polyLine.Count > 0)
                    {
                        Transform(polyLine, newToolPath);
                        polyLine.Clear();
                    }
                    newToolPath.Add(cmd.Token);
                    break;

                case Commands.G1:
                    var s = new Segment
                    {
                        P1 = new Vector3(cmd.Start.X, cmd.Start.Y, 0d),
                        P2 = new Vector3(cmd.End.X, cmd.End.Y, 0d)
                    };
                    if (!s.P1.Equals(s.P2))
                        polyLine.Add(s);
                    break;

                default:
                    newToolPath.Add(cmd.Token);
                    break;
            }
        }

        if (polyLine.Count > 0)
            Transform(polyLine, newToolPath);

        var gc = GCodeParser.TokensToGCode(newToolPath, DragKnifeOptions.AutoCompress);
        var fileName = file.Model?.FileName ?? string.Empty;

        file.AddBlock($"Drag knife transform applied: {fileName}", Action.New);

        foreach (var block in gc)
            file.AddBlock(block, Action.Add);

        file.AddBlock(string.Empty, Action.End);
    }

    static double[] ToPos(Vector3 pos)
    {
        var gcpos = pos.ToArray();
        gcpos[0] = Math.Round(gcpos[0], 3);
        gcpos[1] = Math.Round(gcpos[1], 3);
        gcpos[2] = Math.Round(gcpos[2], 3);
        return gcpos;
    }

    void Transform(List<Segment> polyLine, List<GCodeToken> newToolPath)
    {
        uint lnr = 0;
        var end = polyLine[0].P1;

        var p0 = polyLine[0].P1;
        var prev = new Segment
        {
            P1 = p0,
            P2 = p0 + StartDirection * _knifeTipOffset
        };

        for (int i = 0; i < polyLine.Count; i++)
        {
            var cp1 = prev.P2 - prev.P1;
            var cp2 = polyLine[i].P2 - polyLine[i].P1;
            var n1 = cp1.NormalizeOrDefault();
            var n2 = cp2.NormalizeOrDefault();
            var angle = cp2.Angle(cp1) * (180d / Math.PI);

            if (i == 0)
            {
                end = prev.P1 + n1 * _knifeTipOffset;
                newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(end), AxisFlags.XY, false));
                newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(end + new Vector3(0d, 0d, _cutDepth)), AxisFlags.Z, false));
            }

            if (Math.Abs(angle) > (i == 0 ? 1d : _swivelAngle) && cp2.Magnitude() >= _dentLength)
            {
                var end1 = polyLine[i].P1 + n2 * _knifeTipOffset;
                var dir = (i == 0 ? prev.P1 : prev.P2) - end;
                StartDirection = dir;
                end = end1;
                var arcdir = n1.X * n2.Y - n1.Y * n2.X;
                newToolPath.Add(new GCArc(arcdir < 0d ? Commands.G2 : Commands.G3, lnr++, ToPos(end), AxisFlags.XY, ToPos(dir), IJKFlags.I | IJKFlags.J, 0d, 0, IJKMode.Incremental, false));
            }

            if (cp2.Magnitude() > _knifeTipOffset)
                end = polyLine[i].P2 + n2 * _knifeTipOffset;
            else
                end += cp2;

            newToolPath.Add(new GCLinearMotion(Commands.G1, lnr++, ToPos(end), AxisFlags.XY, false));
            prev = polyLine[i];

            if (i == polyLine.Count - 1)
                newToolPath.Add(new GCLinearMotion(Commands.G0, lnr++, ToPos(end + new Vector3(0d, 0d, -_cutDepth)), AxisFlags.Z, false));
        }
    }
}
