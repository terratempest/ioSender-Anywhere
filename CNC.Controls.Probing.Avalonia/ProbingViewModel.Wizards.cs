using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;

namespace CNC.Controls.Probing;

public sealed partial class ProbingViewModel
{
    readonly ProbingCycleState _cycle = new();
    CenterFindMode _centerFindMode = CenterFindMode.XY;
    int _centerPass;
    int _centerTotalPasses = 1;
    bool _canApplyTransform;
    double _workpieceSizeX;
    double _workpieceSizeY;
    int _passes = 1;
    Center _probeCenter = Center.None;
    ProbeOrigin _origin = ProbeOrigin.None;
    string _probedAngleText = string.Empty;
    ProbingType _activeTab = ProbingType.None;

    public HeightMapViewModel HeightMap { get; } = new();

    public ProbingCycleState Cycle => _cycle;

    public double ProbeRadius => ProbeDiameter / 2d;

    public double WorkpieceSizeX
    {
        get => _workpieceSizeX;
        set { _workpieceSizeX = value; OnPropertyChanged(); }
    }

    public double WorkpieceSizeY
    {
        get => _workpieceSizeY;
        set { _workpieceSizeY = value; OnPropertyChanged(); }
    }

    public int Passes
    {
        get => _passes;
        set { _passes = value; OnPropertyChanged(); }
    }

    public Center ProbeCenter
    {
        get => _probeCenter;
        set { _probeCenter = value; OnPropertyChanged(); }
    }

    public ProbeOrigin Origin
    {
        get => _origin;
        set { _origin = value; OnPropertyChanged(); }
    }

    public bool CanApplyTransform
    {
        get => _canApplyTransform;
        set { _canApplyTransform = value; OnPropertyChanged(); }
    }

    public string ProbedAngleText
    {
        get => _probedAngleText;
        private set { _probedAngleText = value; OnPropertyChanged(); }
    }

    public CenterFindMode CenterFindMode => _centerFindMode;

    public void NotifyProbePositionsChanged()
    {
        OnPropertyChanged(nameof(CameraPositions));
        if (_activeTab == ProbingType.Rotation)
            OnRotationPositionsChanged();
        else if (_activeTab == ProbingType.CenterFinder)
            OnCenterFinderPositionsChanged();
    }

    public void OnProbeTabActivated(ProbingType tab, bool activate)
    {
        _activeTab = activate ? tab : ProbingType.None;
        if (activate)
            ProbingType = tab;

        switch (tab)
        {
            case ProbingType.EdgeFinderInternal:
                if (activate)
                    AllowMeasure = true;
                break;

            case ProbingType.Rotation:
                if (activate)
                {
                    if (ProbeEdge is Edge.A or Edge.B or Edge.C or Edge.D)
                        ProbeEdge = Edge.None;
                    AllowMeasure = false;
                }
                break;

            case ProbingType.CenterFinder:
                if (activate)
                    AllowMeasure = true;
                break;

            case ProbingType.HeightMap:
                if (activate)
                    UpdateHeightMapCanApply();
                break;
        }
    }

    public void CancelProbeCycle()
    {
        _cycle.Cancelled = true;
        Program.Cancel();
    }

    public bool ConfirmInternalOffsetWarning() =>
        GrblUi.AskYesNo(ProbingStrings.OffsetWarning, "ioSender");

    public bool TryStartEdgeFinderInternal(bool preview)
    {
        if (!ValidateInput(ProbeEdge == Edge.Z))
            return false;

        if (ProbeEdge == Edge.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectEdgeType, "Edge finder");
            return false;
        }

        if (!VerifyProbe() || !Program.Init())
            return false;

        _cycle.Cancelled = false;
        if (preview)
            StartPosition.Zero();

        if (ProbeEdge is Edge.A or Edge.B or Edge.C or Edge.D)
        {
            var cornerClearance = XYClearance + ProbeDiameter / 2d;
            if (cornerClearance > Offset && !ConfirmInternalOffsetWarning())
                return false;
        }

        if (!Program.BuildEdgeFinderInternal(ProbeEdge, _cycle))
            return false;

        return RunOrPreview(preview, CompleteEdgeFinderInternal, () =>
        {
            Program.AppendInternalEdgePostProbePreview(_cycle);
        });
    }

    public void CompleteEdgeFinderInternal()
    {
        var ok = IsSuccess && Positions.Count > 0;

        if (ok)
        {
            var p = 0;
            var pos = new Position(StartPosition);

            foreach (var i in _cycle.AxisFlags.ToIndices())
                pos.Values[i] = Positions[p++].Values[i] +
                    (i == GrblConstants.Z_AXIS ? 0d : ProbeDiameter / 2d * _cycle.Af[i]);

            if (double.IsNaN(pos.Z))
            {
                Grbl!.IsJobRunning = false;
                Program.End(ProbingStrings.PositionUnknown);
                return;
            }

            var axisflags = _cycle.AxisFlags;

            if (ProbeZ && axisflags != AxisFlags.Z)
            {
                var pz = new Position(pos);
                var xyOffset = WorkpieceXYEdgeOffset == 0d ? ProbeDiameter / 2d : WorkpieceXYEdgeOffset;
                pz.X += xyOffset * _cycle.Af[GrblConstants.X_AXIS];
                pz.Y += xyOffset * _cycle.Af[GrblConstants.Y_AXIS];

                if ((ok = !_cycle.Cancelled && GotoMachinePosition(pz, axisflags)))
                {
                    ok = ok && !_cycle.Cancelled &&
                          WaitForResponse(FastProbe + "Z-" + Depth.ToInvariantString());
                    ok = ok && !_cycle.Cancelled &&
                          WaitForResponse(RapidCommand + "Z" + LatchDistance.ToInvariantString());
                    ok = ok && !_cycle.Cancelled && RemoveLastPosition();
                    if ((ok = ok && !_cycle.Cancelled &&
                          WaitForResponse(SlowProbe + "Z-" + Depth.ToInvariantString())))
                    {
                        pos.Z = Grbl!.ProbePosition.Z * Grbl.UnitFactor;
                        ok = !_cycle.Cancelled && GotoMachinePosition(StartPosition, AxisFlags.Z);
                    }
                }
            }

            ok = ok && !_cycle.Cancelled && GotoMachinePosition(pos, axisflags);

            if (ProbeZ)
                axisflags |= AxisFlags.Z;

            if (ok)
                ApplyEdgeCoordinateResult(pos, axisflags, internalEdge: true);

            if (axisflags == AxisFlags.Z)
                GotoMachinePosition(StartPosition, AxisFlags.Z);

            Program.End(ok ? ProbingStrings.ProbingCompleted : ProbingStrings.ProbingFailed);
        }

        FinishProbeJob(ok);
    }

    public bool TryStartRotation(bool preview)
    {
        if (!ValidateInput(ProbeEdge == Edge.Z))
            return false;

        if (ProbeEdge == Edge.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectEdgeType, "Rotation");
            return false;
        }

        if (!VerifyProbe() || !Program.Init())
            return false;

        _cycle.Cancelled = false;
        _cycle.ProbedAngle = 0d;
        CanApplyTransform = false;

        if (preview)
            StartPosition.Zero();

        Program.BuildRotation(ProbeEdge, _cycle, includeAddActionPass: AddAction && preview);

        return RunOrPreview(preview, CompleteRotation, null);
    }

    public void CompleteRotation()
    {
        if (!_cycle.Cancelled && (CanApplyTransform = IsSuccess && Positions.Count == 2))
        {
            _cycle.ProbedAngle = ComputeProbedAngle();
            UpdateProbedAngleMessage();

            if (AddAction)
            {
                _cycle.RotationP1.Set(Positions[0]);
                _cycle.RotationP2.Set(Positions[1]);
                Program.BuildRotationAddAction(ProbeEdge, _cycle);
                Program.Execute(true);
                CompleteRotationAddAction();
                return;
            }
        }

        Program.End(CanApplyTransform ? ProbingStrings.ProbingCompleted : ProbingStrings.ProbingFailed,
            Positions.Count != 2);

        if (CanApplyTransform)
            UpdateProbedAngleMessage();

        FinishProbeJob(CanApplyTransform);
    }

    void CompleteRotationAddAction()
    {
        if (!_cycle.Cancelled && (CanApplyTransform = IsSuccess && Positions.Count == 1))
        {
            var p3 = new Position(Positions[0]);
            var p4 = new Position(p3.X + Offset * _cycle.ProbedAngle, p3.Y - Offset, 0d);
            var pos = new Position(StartPosition);

            var p1 = new Position(_cycle.RotationP1);
            var p2 = new Position(_cycle.RotationP2);
            p1.Y += ProbeOffsetY + ProbeRadius;
            p2.Y += ProbeOffsetY + ProbeRadius;
            p3.X += ProbeOffsetX + ProbeRadius;
            p4.X += ProbeOffsetX + ProbeRadius;

            var divisor = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
            pos.X = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.X - p4.X) - (p1.X - p2.X) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;
            pos.Y = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;

            if ((CanApplyTransform = GotoMachinePosition(pos, AxisFlags.XY)))
            {
                switch (CoordinateMode)
                {
                    case CoordMode.G92:
                        pos.X = pos.Y = 0d;
                        WaitForResponse("G92" + pos.ToString(AxisFlags.XY));
                        break;
                    case CoordMode.G10:
                        WaitForResponse(string.Format("G10L2P{0}{1}", CoordinateSystem, pos.ToString(AxisFlags.XY)));
                        break;
                }
            }
        }
    }

    public void OnRotationPositionsChanged()
    {
        if (CameraPositions == 1 && ProbeEdge == Edge.None)
            PreviewText += "\n; Select edge AB, AD, CB or CD for second probe.";

        if (CameraPositions == 2 && ProbeEdge != Edge.None)
        {
            _cycle.ProbedAngle = ComputeProbedAngle();
            CanApplyTransform = true;
            UpdateProbedAngleMessage();
        }
    }

    double ComputeProbedAngle()
    {
        if (Positions.Count < 2 || Grbl == null)
            return 0d;

        var angle = (Positions[1].Y - Positions[0].Y) / (Positions[1].X - Positions[0].X);
        if (ProbeEdge is Edge.CB or Edge.AD)
            angle = -1.0d / angle;

        _cycle.ProbedAngle = angle;
        return angle;
    }

    void UpdateProbedAngleMessage()
    {
        var degrees = Math.Round(Math.Atan(_cycle.ProbedAngle) * 180d / Math.PI, 3);
        ProbedAngleText = string.Format(ProbingStrings.ProbedAngle, degrees.ToInvariantString());
        if (Grbl != null)
            Message = ProbedAngleText;
    }

    public void TryApplyRotationToGCode()
    {
        if (Grbl == null || !Grbl.IsFileLoaded)
        {
            GrblUi.ShowError(ProbingStrings.NoFileForTransform, "G-code Rotate");
            return;
        }

        if (!CanApplyTransform)
            return;

        try
        {
            var compress = ControlsPlatformContext.AppConfig?.Base.AutoCompress ?? false;
            new GCodeRotate().ApplyRotation(_cycle.ProbedAngle, GetRotationOriginOffset(), compress);
        }
        catch (Exception ex)
        {
            GrblUi.ShowError(ex.Message, "G-code Rotate");
        }
    }

    Vector3 GetRotationOriginOffset()
    {
        if (Grbl == null || !Grbl.IsFileLoaded)
        {
            Origin = ProbeOrigin.None;
            return new Vector3();
        }

        var limits = Grbl.ProgramLimits;
        return Origin switch
        {
            ProbeOrigin.A => new Vector3(limits.MinX, limits.MinY, 0d),
            ProbeOrigin.B => new Vector3(limits.MaxX, limits.MinY, 0d),
            ProbeOrigin.C => new Vector3(limits.MaxX, limits.MaxY, 0d),
            ProbeOrigin.D => new Vector3(limits.MinX, limits.MaxY, 0d),
            ProbeOrigin.Center => new Vector3(limits.MaxX / 2d, limits.MaxY / 2d, 0d),
            ProbeOrigin.AB => new Vector3(limits.MaxX / 2d, limits.MinY, 0d),
            ProbeOrigin.AD => new Vector3(limits.MinX, limits.MaxY / 2d, 0d),
            ProbeOrigin.CB => new Vector3(limits.MaxX, limits.MaxY / 2d, 0d),
            ProbeOrigin.CD => new Vector3(limits.MaxX / 2d, limits.MaxY, 0d),
            ProbeOrigin.CurrentPos => new Vector3(Grbl.Position.X, Grbl.Position.Y, 0d),
            _ => new Vector3()
        };
    }

    public bool HeightMapApplied
    {
        get => GCodeFileService.Instance.HeightMapApplied;
        set
        {
            if (GCodeFileService.Instance.HeightMapApplied == value)
                return;
            GCodeFileService.Instance.HeightMapApplied = value;
            OnPropertyChanged();
            UpdateHeightMapCanApply();
        }
    }

    public bool TryStartCenterFinder(bool preview)
    {
        if (!ValidateInput(false) || Passes == 0)
            return false;

        _centerFindMode = CenterFindMode.XY;
        if (WorkpieceSizeX <= 0d && WorkpieceSizeY <= 0d)
        {
            SetError(nameof(WorkpieceSizeX), string.Format(ProbingStrings.WorkpieceSizeRequired, "X"));
            SetError(nameof(WorkpieceSizeY), string.Format(ProbingStrings.WorkpieceSizeRequired, "Y"));
            return false;
        }

        if (WorkpieceSizeX <= 0d)
            _centerFindMode = CenterFindMode.Y;
        if (WorkpieceSizeY <= 0d)
            _centerFindMode = CenterFindMode.X;

        if (_centerFindMode != CenterFindMode.Y && ProbeCenter == Center.Inside &&
            WorkpieceSizeX < XYClearance * 2d + ProbeDiameter)
        {
            SetError(nameof(WorkpieceSizeX), string.Format(ProbingStrings.ClearanceTooLarge, "X"));
            return false;
        }

        if (_centerFindMode != CenterFindMode.X && ProbeCenter == Center.Inside &&
            WorkpieceSizeY < XYClearance * 2d + ProbeDiameter)
        {
            SetError(nameof(WorkpieceSizeY), string.Format(ProbingStrings.ClearanceTooLarge, "Y"));
            return false;
        }

        if (ProbeCenter == Center.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectCenterType, "Center finder");
            return false;
        }

        if (!VerifyProbe())
            return false;

        _centerPass = preview ? 1 : Passes;
        _centerTotalPasses = Passes;
        _cycle.Cancelled = false;
        CanApplyTransform = false;

        return RunCenterFinderPasses(preview);
    }

    bool RunCenterFinderPasses(bool preview)
    {
        do
        {
            if (!BuildCenterFinderProgram(preview))
                return false;

            if (preview)
            {
                PreviewText = Program.ToString().Replace("G53", string.Empty);
                Program.Clear();
                Program.BuildCenterFinder(ProbeCenter, _centerFindMode, _centerPass, _centerTotalPasses);
                Program.AppendCenterFinderPostProbePreview(_centerFindMode);
                PreviewText += "\n; Post XY probe\n" + Program.ToString().Replace("G53", string.Empty);
                return true;
            }

            Program.Execute(true);
            if (!CompleteCenterFinderPass())
                return false;

            WaitForIdle(string.Empty);
        } while (--_centerPass != 0 && BuildCenterFinderProgram(preview));

        return true;
    }

    bool BuildCenterFinderProgram(bool preview)
    {
        if (!Program.Init())
        {
            Message = ProbingStrings.InitFailed + " " + Message;
            return false;
        }

        if (preview)
            StartPosition.Zero();

        Program.BuildCenterFinder(ProbeCenter, _centerFindMode, _centerPass, _centerTotalPasses);

        if (Passes > 1)
            Message = string.Format(ProbingStrings.ProbingPass, _centerTotalPasses - _centerPass + 1, _centerTotalPasses);

        return true;
    }

    bool CompleteCenterFinderPass()
    {
        var expected = _centerFindMode == CenterFindMode.XY ? 4 : 2;

        if (IsSuccess && Positions.Count != expected)
        {
            IsSuccess = false;
            Program.End(ProbingStrings.ProbingFailed, true);
            return false;
        }

        if (!IsSuccess)
        {
            Program.End(ProbingStrings.ProbingFailed);
            Program.OnCompleted?.Invoke(false);
            return false;
        }

        if (_centerPass > 1)
            return true;

        var ok = true;
        var axisflags = _centerFindMode switch
        {
            CenterFindMode.X => AxisFlags.X,
            CenterFindMode.Y => AxisFlags.Y,
            _ => AxisFlags.XY
        };

        var center = new Position(StartPosition);
        double xDistance, yDistance;

        center.X = _centerFindMode != CenterFindMode.Y
            ? Positions[0].X + (Positions[1].X - Positions[0].X) / 2d
            : 0d;
        xDistance = _centerFindMode != CenterFindMode.Y
            ? Math.Abs(Positions[1].X - Positions[0].X)
            : 0d;

        if (_centerFindMode == CenterFindMode.XY)
        {
            center.Y = Positions[2].Y + (Positions[3].Y - Positions[2].Y) / 2d;
            yDistance = Math.Abs(Positions[3].Y - Positions[2].Y);
        }
        else
        {
            center.Y = _centerFindMode != CenterFindMode.X
                ? Positions[0].Y + (Positions[1].Y - Positions[0].Y) / 2d
                : 0d;
            yDistance = _centerFindMode != CenterFindMode.X
                ? Math.Abs(Positions[0].Y - Positions[1].Y)
                : 0d;
        }

        switch (ProbeCenter)
        {
            case Center.Inside:
                if (_centerFindMode != CenterFindMode.Y)
                    xDistance += ProbeDiameter;
                if (_centerFindMode != CenterFindMode.X)
                    yDistance += ProbeDiameter;
                break;
            case Center.Outside:
                if (_centerFindMode != CenterFindMode.Y)
                    xDistance -= ProbeDiameter;
                if (_centerFindMode != CenterFindMode.X)
                    yDistance -= ProbeDiameter;
                break;
        }

        ok = ok && GotoMachinePosition(center, axisflags);

        if (ok)
        {
            switch (CoordinateMode)
            {
                case CoordMode.Measure:
                    center.X += ProbeOffsetX;
                    center.Y += ProbeOffsetY;
                    Measurement.Add(new Position(center, 1d / Grbl!.UnitFactor), axisflags, ProbingType.CenterFinder);
                    break;
                case CoordMode.G92:
                    center.X = ProbeOffsetX;
                    center.Y = ProbeOffsetY;
                    WaitForResponse("G92" + center.ToString(axisflags));
                    if (!Grbl!.IsParserStateLive)
                        Grbl.ExecuteCommand("$G");
                    break;
                case CoordMode.G10:
                    center.X += ProbeOffsetX;
                    center.Y += ProbeOffsetY;
                    WaitForResponse(string.Format("G10L2P{0}{1}", CoordinateSystem, center.ToString(axisflags)));
                    break;
            }

            Program.End(string.Format(ProbingStrings.CenterCompleted,
                xDistance.ToInvariantString(), yDistance.ToInvariantString()));
        }
        else
            Program.End(ProbingStrings.ProbingFailed);

        CanApplyTransform = ok;
        Program.OnCompleted?.Invoke(ok);
        return ok;
    }

    public void OnCenterFinderPositionsChanged()
    {
        var expected = _centerFindMode == CenterFindMode.XY ? 4 : 2;
        var ok = CameraPositions <= 1 ||
                 !Positions[CameraPositions - 1].Equals(Positions[CameraPositions - 2]);

        if (ok)
        {
            ok = CameraPositions switch
            {
                2 => _centerFindMode == CenterFindMode.Y
                    ? Positions[1].X == Positions[0].X && Positions[1].Y > Positions[0].Y
                    : Positions[1].X > Positions[0].X && Positions[1].Y == Positions[0].Y,
                3 => !Positions[2].Equals(Positions[0]) && Positions[2].X != Positions[1].X,
                4 => !Positions[3].Equals(Positions[0]) &&
                     !Positions[3].Equals(Positions[1]) &&
                     Positions[3].X == Positions[2].X &&
                     Positions[3].Y > Positions[2].Y,
                _ => true
            };
        }

        if (!ok)
        {
            GrblUi.ShowError(ProbingStrings.IllegalPosition, "ioSender");
            RemoveLastPosition();
        }
        else
            CanApplyTransform = CameraPositions == expected;
    }

    public bool TryCompleteCenterFinderFromCamera()
    {
        var expected = _centerFindMode == CenterFindMode.XY ? 4 : 2;
        if (ProbeCenter == Center.None || Positions.Count != expected)
            return false;

        IsSuccess = true;
        _centerPass = 1;
        return CompleteCenterFinderPass();
    }

    public bool TryStartHeightMap(bool confirmOriginMove = true)
    {
        if (!ValidateInput(true) || Grbl == null)
            return false;

        var startpos = new Position(
            HeightMap.MinX - ProbeOffsetX,
            HeightMap.MinY - ProbeOffsetY,
            0d);

        if (confirmOriginMove &&
            (Math.Abs(startpos.X - Grbl.Position.X) > 0.01d || Math.Abs(startpos.Y - Grbl.Position.Y) > 0.01d) &&
            !GrblUi.AskYesNo(
                string.Format(ProbingStrings.AreaOriginConfirm,
                    startpos.X.ToInvariantString(Grbl.Format),
                    startpos.Y.ToInvariantString(Grbl.Format)),
                "ioSender"))
            return false;

        _cycle.HeightMapOrigin.Set(new Position(Grbl.MachinePosition, Grbl.UnitFactor));

        if (!WaitForIdle(string.Format("G90G0X{0}Y{1}",
                startpos.X.ToInvariantString(Grbl.Format),
                startpos.Y.ToInvariantString(Grbl.Format))))
            return false;

        if (!VerifyProbe() || !Program.Init())
            return false;

        HeightMap.Map = null;
        HeightMap.HasHeightMap = false;
        HeightMap.Preview = new HeightMapPreview();
        Message = string.Empty;

        try
        {
            var map = new HeightMap(
                HeightMap.GridSizeX,
                HeightMap.GridSizeY,
                new Vector2(HeightMap.MinX, HeightMap.MinY),
                new Vector2(HeightMap.MaxX, HeightMap.MaxY));
            HeightMap.Map = map;
        }
        catch (Exception ex)
        {
            Message = ex.Message;
            return false;
        }

        var heightMap = HeightMap.Map;
        if (heightMap == null)
            return false;

        HeightMap.GridSizeLockXY = Math.Abs(heightMap.GridX - heightMap.GridY) < double.Epsilon;
        HeightMap.GridSizeX = heightMap.GridX;
        HeightMap.GridSizeY = heightMap.GridY;
        HeightMap.RefreshPreview();

        Program.BuildHeightMap(heightMap, HeightMap.AddPause);
        Program.Execute(true);
        CompleteHeightMap();
        return true;
    }

    public void CompleteHeightMap()
    {
        var map = HeightMap.Map;
        var grbl = Grbl;
        var ok = IsSuccess && map != null && grbl != null && Positions.Count == map.TotalPoints;

        if (ok)
        {
            var activeMap = map!;
            var activeGrbl = grbl!;
            var z0 = Positions[0].Z;
            var zMin = 0d;
            var zMax = 0d;
            var i = 0;
            for (var x = 0; x < activeMap.SizeX; x++)
            {
                for (var y = 0; y < activeMap.SizeY; y++)
                {
                    var zDelta = Positions[i++].Z - z0;
                    zMin = Math.Min(zMin, zDelta);
                    zMax = Math.Max(zMax, zDelta);
                    activeMap.AddPoint(x, y, Math.Round(zDelta, activeGrbl.Precision));
                }

                if (++x < activeMap.SizeX)
                {
                    for (var y = activeMap.SizeY - 1; y >= 0; y--)
                    {
                        var zDelta = Positions[i++].Z - z0;
                        zMin = Math.Min(zMin, zDelta);
                        zMax = Math.Max(zMax, zDelta);
                        activeMap.AddPoint(x, y, Math.Round(zDelta, activeGrbl.Precision));
                    }
                }
            }

            HeightMap.Preview = activeMap.BuildPreview();
            HeightMap.HasHeightMap = true;
            UpdateHeightMapCanApply();

            if (HeightMap.SetToolOffset &&
                Program.ProbeZ(-ProbeOffsetX, -ProbeOffsetY))
            {
                activeMap.ZOffset = Math.Round(z0 - Positions[0].Z, activeGrbl.Precision);

                if (CoordinateMode == CoordMode.G10)
                    activeGrbl.ExecuteCommand(string.Format("G10L2P{0}Z{1}", CoordinateSystem,
                        (Positions[0].Z - activeGrbl.ToolOffset.Z).ToInvariantString()));
                else if (GotoMachinePosition(Positions[0], AxisFlags.Z))
                {
                    activeGrbl.ExecuteCommand("G92Z0");
                    if (!activeGrbl.IsParserStateLive)
                        activeGrbl.ExecuteCommand("$G");
                }
            }

            GotoMachinePosition(_cycle.HeightMapOrigin, AxisFlags.Z);
            GotoMachinePosition(_cycle.HeightMapOrigin, AxisFlags.X | AxisFlags.Y);

            Program.End(string.Format(ProbingStrings.HeightMapCompleted,
                zMin.ToInvariantString(activeGrbl.Format),
                zMax.ToInvariantString(activeGrbl.Format)));
        }
        else
            Program.End(ProbingStrings.ProbingFailed, map != null && Positions.Count != map.TotalPoints);

        Program.OnCompleted?.Invoke(ok);
        FinishProbeJob(ok);
    }

    public void LoadHeightMap(string fileName)
    {
        HeightMap.HasHeightMap = false;
        var map = global::CNC.Controls.Probing.HeightMap.Load(fileName);
        HeightMap.Map = map;
        HeightMap.GridSizeLockXY = Math.Abs(map.GridX - map.GridY) < double.Epsilon;
        HeightMap.GridSizeX = map.GridX;
        HeightMap.GridSizeY = map.GridY;
        HeightMap.MinX = map.Min.X;
        HeightMap.MinY = map.Min.Y;
        HeightMap.MaxX = map.Max.X;
        HeightMap.MaxY = map.Max.Y;
        HeightMap.Preview = map.BuildPreview();
        HeightMap.HasHeightMap = true;
        UpdateHeightMapCanApply();
    }

    public void SaveHeightMap(string fileName) => HeightMap.Map?.Save(fileName);

    public void TryApplyHeightMapToGCode()
    {
        if (Grbl == null || !Grbl.IsFileLoaded || HeightMap.Map == null)
        {
            GrblUi.ShowError(ProbingStrings.NoFileForTransform, "Heightmap");
            return;
        }

        try
        {
            new GCodeTransform().ApplyHeightMap(this);
            HeightMapApplied = true;
        }
        catch (Exception ex)
        {
            GrblUi.ShowError(ex.Message, "Heightmap");
        }
    }

    public void SetHeightMapFromProgramLimits()
    {
        if (Grbl == null)
            return;

        var limits = new ProgramLimits(Grbl.ProgramLimits, Grbl.UnitFactor);
        HeightMap.MinX = limits.MinX;
        HeightMap.MinY = limits.MinY;
        HeightMap.MaxX = limits.MaxX;
        HeightMap.MaxY = limits.MaxY;
        HeightMap.RefreshPreview();
    }

    void UpdateHeightMapCanApply() =>
        HeightMap.CanApply = HeightMap.HasHeightMap && !HeightMapApplied && Grbl is { IsFileLoaded: true };

    bool RunOrPreview(bool preview, System.Action onComplete, System.Action? appendPreview)
    {
        if (preview)
        {
            PreviewText = Program.ToString().Replace("G53", string.Empty);
            if (appendPreview != null)
            {
                Program.Clear();
                appendPreview();
                PreviewText += "\n; Post XY probe\n" + Program.ToString().Replace("G53", string.Empty);
            }

            return true;
        }

        Program.Execute(true);
        onComplete();
        return IsSuccess;
    }

    void ApplyEdgeCoordinateResult(Position pos, AxisFlags axisflags, bool internalEdge)
    {
        switch (CoordinateMode)
        {
            case CoordMode.Measure:
                if (internalEdge)
                {
                    pos.X -= ProbeOffsetX;
                    pos.Y -= ProbeOffsetY;
                }
                else
                {
                    pos.X += ProbeTPOffsetX;
                    pos.Y += ProbeTPOffsetY;
                }

                pos.Z -= WorkpieceHeight + TouchPlateHeight + Grbl!.ToolOffset.Z;
                Measurement.Add(new Position(pos, 1d / Grbl.UnitFactor), axisflags, ProbingType);
                break;

            case CoordMode.G92:
                if (GotoMachinePosition(pos, AxisFlags.Z))
                {
                    if (internalEdge)
                    {
                        pos.X = ProbeOffsetX;
                        pos.Y = ProbeOffsetY;
                    }
                    else
                    {
                        pos.X = -ProbeTPOffsetX;
                        pos.Y = -ProbeTPOffsetY;
                    }

                    pos.Z = WorkpieceHeight + TouchPlateHeight;
                    WaitForResponse("G92" + pos.ToString(axisflags));
                    if (!_cycle.Cancelled && axisflags.HasFlag(AxisFlags.Z))
                        GotoMachinePosition(StartPosition, AxisFlags.Z);
                }
                break;

            case CoordMode.G10:
                if (internalEdge)
                {
                    pos.X -= ProbeTPOffsetX;
                    pos.Y -= ProbeTPOffsetY;
                }
                else
                {
                    pos.X += ProbeTPOffsetX;
                    pos.Y += ProbeTPOffsetY;
                }

                pos.Z -= WorkpieceHeight + TouchPlateHeight + Grbl!.ToolOffset.Z;
                WaitForResponse(string.Format("G10L2P{0}{1}", CoordinateSystem, pos.ToString(axisflags)));
                break;
        }
    }

    void FinishProbeJob(bool ok)
    {
        if (Grbl != null && !Grbl.IsParserStateLive && CoordinateMode == CoordMode.G92)
            Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

        if (Grbl != null)
            Grbl.IsJobRunning = false;

        Program.OnCompleted?.Invoke(ok);
    }
}
