using System.IO;
using Avalonia.Controls;
using CNC.Core;
using CNC.Core.Geometry;

namespace CNC.Converters;

public class Excellon2GCode : IGCodeConverter
{
    public struct ExcellonCommand
    {
        public string Command;
        public int tool;
        public Point3D Start;
        public Point3D End;
    }

    enum Command
    {
        M48,
        M71,
        M72,
        M95,
        G05
    }

    bool isDrillMode = true, isMetric = true;
    bool? isHeader;
    readonly List<JobParametersViewModel.Tool> tools = [];
    readonly List<ExcellonCommand> commands = [];
    IGCodeFileTarget job = null!;
    Point3D lastPos;
    readonly JobParametersViewModel settings = new();

    public string FileType => "Excellon files";
    public string FileExtensions => "drl,xln";

    public bool LoadFile(IGCodeFileTarget job, string filename, Window? owner)
    {
        this.job = job;
        settings.Profile = "Excellon";

        if (!JobParametersDialog.Show(settings, owner))
            return false;

        bool ok = true, leadingZeros = false;
        double scaleFactor = 0d;
        var tool = new JobParametersViewModel.Tool { Id = 0, Diameter = 0d };

        using var sr = new FileInfo(filename).OpenText();
        string? s = sr.ReadLine();

        while (s != null)
        {
            try
            {
                s = s.Trim();

                if (isHeader == null)
                    isHeader = s == "M48";
                else if (isHeader == true)
                {
                    var hdr = s.Split(',');

                    switch (hdr[0])
                    {
                        case "METRIC":
                            isMetric = true;
                            leadingZeros = hdr.Length > 1 && hdr[1] == "TZ";
                            if (hdr.Length > 2)
                            {
                                var scale = hdr[2].Split('.');
                                if (scale.Length == 2)
                                    scaleFactor = dbl.Parse("1" + scale[1]);
                            }
                            break;

                        case "INCH":
                            isMetric = false;
                            leadingZeros = hdr.Length > 1 && hdr[1] == "TZ";
                            if (hdr.Length > 2)
                            {
                                var scale = hdr[2].Split('.');
                                if (scale.Length == 2)
                                    scaleFactor = dbl.Parse("1" + scale[1]);
                            }
                            break;

                        case "%":
                        case "M95":
                            isHeader = false;
                            break;
                    }

                    if (s.Length > 0 && s[0] == 'T')
                    {
                        int cpos = s.IndexOf('C');
                        tools.Add(new JobParametersViewModel.Tool
                        {
                            Id = int.Parse(s.Substring(1, cpos - 1)),
                            Diameter = dbl.Parse(s.Substring(cpos + 1))
                        });
                    }
                }
                else
                {
                    switch (s[0])
                    {
                        case 'G':
                            switch (s)
                            {
                                case "G00":
                                case "G01":
                                case "G02":
                                case "G03":
                                    isDrillMode = false;
                                    break;
                                case "G05":
                                    isDrillMode = true;
                                    break;
                            }
                            break;

                        case 'X':
                            if (isDrillMode)
                            {
                                int g85pos = s.IndexOf("G85", StringComparison.Ordinal);
                                string args = g85pos >= 0 ? s[..g85pos] : s;
                                int ypos = args.IndexOf('Y');
                                double factor = scaleFactor == 0d ? (leadingZeros ? (ypos == 8 ? 1000d : 10000d) : 1.0) : scaleFactor;
                                var cmd = new ExcellonCommand
                                {
                                    Command = g85pos >= 0 ? "Slot" : "Drill",
                                    tool = tool.Id,
                                    Start = new Point3D(
                                        dbl.Parse(args.Substring(1, ypos - 1)) / factor / (isMetric ? 1d : 25.4d),
                                        dbl.Parse(args[(ypos + 1)..]) / factor / (isMetric ? 1d : 25.4d),
                                        0d)
                                };
                                if (g85pos >= 0)
                                {
                                    isDrillMode = false;
                                    args = s[(g85pos + 3)..];
                                    ypos = args.IndexOf('Y');
                                    cmd.End = new Point3D(
                                        dbl.Parse(args.Substring(1, ypos - 1)) / (isMetric ? 1d : 25.4d),
                                        dbl.Parse(args[(ypos + 1)..]) / (isMetric ? 1d : 25.4d),
                                        0d);
                                }
                                commands.Add(cmd);
                            }
                            break;

                        case 'T':
                            tool = tools.FirstOrDefault(x => x.Id == int.Parse(s[1..]));
                            break;
                    }
                }
            }
            catch
            {
            }

            s = sr.ReadLine();
        }

        job.AddBlock(filename, CNC.Core.Action.New);
        job.AddBlock("(Translated by Excellon to GCode converter)");
        job.AddBlock("G90G17G21G50");

        if (settings.ScaleX != 1d || settings.ScaleY != 1d)
            job.AddBlock($"G51X{settings.ScaleX.ToInvariantString()}Y{settings.ScaleY.ToInvariantString()}");

        job.AddBlock("G0Z" + settings.ZRapids.ToInvariantString());
        job.AddBlock("X0Y0");

        var target = new Point3D(0d, 0d, settings.ZRapids);

        foreach (var t in tools)
        {
            job.AddBlock("M5");
            target = new Point3D(0d, 0d, settings.ZHome);
            job.AddBlock("G0" + PosToParams(target));
            job.AddBlock($"M6 (MSG, {(t.Diameter < settings.ToolDiameter ? t.Diameter : settings.ToolDiameter).ToInvariantString()} mm {(t.Diameter < settings.ToolDiameter ? "drill" : "mill")})");
            job.AddBlock("M3S" + settings.RPM.ToInvariantString());
            job.AddBlock("G4P1");
            target = new Point3D(0d, 0d, settings.ZRapids);
            job.AddBlock("G0" + PosToParams(target));
            job.AddBlock("F" + settings.PlungeRate.ToInvariantString());

            foreach (var cmd in commands)
            {
                if (cmd.tool != t.Id)
                    continue;

                switch (cmd.Command)
                {
                    case "Drill":
                        if (t.Diameter > settings.ToolDiameter)
                        {
                            double r = (t.Diameter - settings.ToolDiameter) / 2d;
                            target = new Point3D(cmd.Start.X + r, cmd.Start.Y, settings.ZRapids);
                            var p = PosToParams(target);
                            if (p.Length > 0)
                                job.AddBlock("G0" + p);
                            target = new Point3D(cmd.Start.X + r, cmd.Start.Y, settings.ZMin);
                            job.AddBlock("G1" + PosToParams(target));
                            job.AddBlock($"G2X{target.X.ToInvariantString()}I-{r.ToInvariantString()}");
                            target = new Point3D(cmd.Start.X + r, cmd.Start.Y, settings.ZRapids);
                            job.AddBlock("G0" + PosToParams(target));
                        }
                        else
                            OutputG81(new Point3D(cmd.Start.X, cmd.Start.Y, settings.ZMin));
                        break;
                    case "Slot":
                        OutputSlot(cmd, t.Diameter);
                        break;
                }
            }
        }

        job.AddBlock("G0X0Y0Z" + settings.ZHome.ToInvariantString());
        job.AddBlock("M30", CNC.Core.Action.End);
        return ok;
    }

    string PosToParams(Point3D pos)
    {
        string gcode = string.Empty;

        if (pos.X != lastPos.X)
            gcode += "X" + Math.Round(pos.X, 3).ToInvariantString();

        if (pos.Y != lastPos.Y)
            gcode += "Y" + Math.Round(pos.Y, 3).ToInvariantString();

        if (pos.Z != lastPos.Z)
            gcode += "Z" + Math.Round(pos.Z, 3).ToInvariantString();

        lastPos = pos;
        return gcode;
    }

    void OutputSlot(ExcellonCommand cmd, double tsize)
    {
        double x = cmd.End.X - cmd.Start.X;
        double y = cmd.End.Y - cmd.Start.Y;
        double dist = Math.Sqrt(x * x + y * y);
        int holes = (int)Math.Round(dist / (tsize / 3d) + 0.5d, 0);
        double factor = dist / (holes - 1);
        x = x / dist * factor;
        y = y / dist * factor;

        job.AddBlock($"(Slot {cmd.Start.X.ToInvariantString()};{cmd.Start.Y.ToInvariantString()} - {cmd.End.X.ToInvariantString()};{cmd.End.Y.ToInvariantString()})");

        var target = new Point3D(cmd.Start.X, cmd.Start.Y, settings.ZMin);
        OutputG81(target);

        while (--holes > 0)
        {
            target = new Point3D(target.X + x, target.Y + y, target.Z);
            OutputG81(target);
        }

        lastPos = target;
    }

    void OutputG81(Point3D pos)
    {
        var p = PosToParams(pos);
        if (p.Length > 0)
            job.AddBlock("G81" + p + "R" + settings.ZSafe.ToInvariantString());
    }
}
