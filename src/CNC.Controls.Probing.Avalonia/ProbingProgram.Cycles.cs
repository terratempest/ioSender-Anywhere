using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class ProbingProgram
{
    public bool BuildEdgeFinderInternal(Edge edge, ProbingCycleState state)
    {
        Clear();
        var xyClearance = _probing.XYClearance + _probing.ProbeDiameter / 2d;
        Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));

        switch (edge)
        {
            case Edge.A:
                return AddInternalCorner(state, true, true, xyClearance);
            case Edge.B:
                return AddInternalCorner(state, false, true, xyClearance);
            case Edge.C:
                return AddInternalCorner(state, false, false, xyClearance);
            case Edge.D:
                return AddInternalCorner(state, true, false, xyClearance);
            case Edge.Z:
                state.AxisFlags = AxisFlags.Z;
                state.Af[GrblConstants.Z_AXIS] = 1d;
                AddProbingAction(AxisFlags.Z, true);
                return true;
            case Edge.AD:
                AddInternalEdge(state, 'X', true, xyClearance);
                return true;
            case Edge.AB:
                AddInternalEdge(state, 'Y', true, xyClearance);
                return true;
            case Edge.CB:
                AddInternalEdge(state, 'X', false, xyClearance);
                return true;
            case Edge.CD:
                AddInternalEdge(state, 'Y', false, xyClearance);
                return true;
            default:
                return false;
        }
    }

    bool AddInternalCorner(ProbingCycleState state, bool negx, bool negy, double xyClearance)
    {
        state.Af[GrblConstants.X_AXIS] = negx ? -1d : 1d;
        state.Af[GrblConstants.Y_AXIS] = negy ? -1d : 1d;
        state.AxisFlags = AxisFlags.X | AxisFlags.Y;

        xyClearance = Math.Min(xyClearance, _probing.Offset);

        var rapidto = new Position(_probing.StartPosition);
        rapidto.X -= xyClearance * state.Af[GrblConstants.X_AXIS];
        rapidto.Y -= _probing.Offset * state.Af[GrblConstants.Y_AXIS];
        rapidto.Z -= _probing.Depth;

        AddRapidToMPos(rapidto, AxisFlags.X | AxisFlags.Y);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(AxisFlags.X, negx);
        AddRapidToMPos(rapidto, AxisFlags.X);
        rapidto.X = _probing.StartPosition.X - _probing.Offset * state.Af[GrblConstants.X_AXIS];
        rapidto.Y = _probing.StartPosition.Y - xyClearance * state.Af[GrblConstants.Y_AXIS];
        AddRapidToMPos(rapidto, AxisFlags.X | AxisFlags.Y);
        AddProbingAction(AxisFlags.Y, negy);
        AddRapidToMPos(rapidto, AxisFlags.Y);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        return true;
    }

    void AddInternalEdge(ProbingCycleState state, char axisletter, bool negative, double xyClearance)
    {
        var axis = GrblInfo.AxisLetterToIndex(axisletter);
        state.Af[axis] = negative ? -1d : 1d;
        state.AxisFlags = GrblInfo.AxisLetterToFlag(axisletter);

        var rapidto = new Position(_probing.StartPosition);
        rapidto.Values[axis] -= xyClearance * state.Af[axis];
        rapidto.Z -= _probing.Depth;

        AddRapidToMPos(rapidto, state.AxisFlags);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(state.AxisFlags, negative);
        rapidto.Values[axis] = _probing.StartPosition.Values[axis] - xyClearance * state.Af[axis];
        AddRapidToMPos(rapidto, state.AxisFlags);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
    }

    public void BuildRotation(Edge edge, ProbingCycleState state, bool includeAddActionPass)
    {
        Clear();
        var xyClearance = _probing.XYClearance + _probing.ProbeRadius;
        Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));

        switch (edge)
        {
            case Edge.AD:
                state.Af[GrblConstants.X_AXIS] = 1.0d;
                state.Af[GrblConstants.Y_AXIS] = -1.0d;
                AddRotationEdge(state, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.AB:
                state.Af[GrblConstants.X_AXIS] = 1.0d;
                state.Af[GrblConstants.Y_AXIS] = 1.0d;
                AddRotationEdge(state, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                if (includeAddActionPass)
                    AddRotationActionEdge(state, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.CB:
                state.Af[GrblConstants.X_AXIS] = -1.0d;
                state.Af[GrblConstants.Y_AXIS] = 1.0d;
                AddRotationEdge(state, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.CD:
                state.Af[GrblConstants.X_AXIS] = -1.0d;
                state.Af[GrblConstants.Y_AXIS] = -1.0d;
                AddRotationEdge(state, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                break;
        }
    }

    public void BuildRotationAddAction(Edge edge, ProbingCycleState state)
    {
        Clear();
        Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));
        var xyClearance = _probing.XYClearance + _probing.ProbeRadius;

        switch (edge)
        {
            case Edge.AD:
                state.Af[GrblConstants.X_AXIS] = 1.0d;
                state.Af[GrblConstants.Y_AXIS] = -1.0d;
                AddRotationActionEdge(state, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                break;
            case Edge.AB:
                state.Af[GrblConstants.X_AXIS] = 1.0d;
                state.Af[GrblConstants.Y_AXIS] = 1.0d;
                AddRotationActionEdge(state, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.CB:
                state.Af[GrblConstants.X_AXIS] = -1.0d;
                state.Af[GrblConstants.Y_AXIS] = 1.0d;
                AddRotationActionEdge(state, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                break;
            case Edge.CD:
                state.Af[GrblConstants.X_AXIS] = -1.0d;
                state.Af[GrblConstants.Y_AXIS] = -1.0d;
                AddRotationActionEdge(state, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
        }
    }

    void AddRotationEdge(ProbingCycleState state, int offsetAxis, int clearanceAxis, double xyClearance)
    {
        var probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
        var rapidto = new Position(_probing.StartPosition);

        rapidto.Values[clearanceAxis] -= xyClearance * state.Af[clearanceAxis];
        rapidto.Z -= _probing.Depth;

        AddRapidToMPos(rapidto, probeAxis);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(probeAxis, state.Af[clearanceAxis] == -1.0d);
        AddRapidToMPos(rapidto, probeAxis);
        rapidto.Values[offsetAxis] = _probing.StartPosition.Values[offsetAxis] + _probing.Offset * state.Af[offsetAxis];
        AddRapidToMPos(rapidto, GrblInfo.AxisIndexToFlag(offsetAxis));
        AddProbingAction(probeAxis, state.Af[clearanceAxis] == -1.0d);
        AddRapidToMPos(rapidto, probeAxis);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.XY);
    }

    void AddRotationActionEdge(ProbingCycleState state, int offsetAxis, int clearanceAxis, double xyClearance)
    {
        var probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
        var rapidto = new Position(_probing.StartPosition);

        rapidto.Values[clearanceAxis] -= xyClearance * state.Af[clearanceAxis];
        rapidto.Values[offsetAxis] += _probing.Offset * state.Af[offsetAxis];
        rapidto.Z -= _probing.Depth;

        AddRapidToMPos(rapidto, AxisFlags.XY);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(probeAxis, state.Af[clearanceAxis] == -1.0d);
        AddRapidToMPos(rapidto, probeAxis);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
    }

    public void BuildCenterFinder(Center center, CenterFindMode mode, int pass, int totalPasses)
    {
        if (pass == totalPasses)
            Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));

        var rapidto = new Position(_probing.StartPosition);
        var xyClearance = _probing.XYClearance + _probing.ProbeDiameter / 2d;
        rapidto.Z -= _probing.Depth;

        switch (center)
        {
            case Center.Inside:
                AddRapidToMPos(rapidto, AxisFlags.Z);
                if (mode != CenterFindMode.Y)
                    AddCenterInsideX(rapidto, xyClearance);
                if (mode != CenterFindMode.X)
                    AddCenterInsideY(rapidto, xyClearance);
                AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
                break;

            case Center.Outside:
                rapidto.X -= _probing.WorkpieceSizeX / 2d + xyClearance;
                rapidto.Y -= _probing.WorkpieceSizeY / 2d + xyClearance;
                if (mode != CenterFindMode.Y)
                    AddCenterOutsideX(rapidto, xyClearance);
                if (mode != CenterFindMode.X)
                    AddCenterOutsideY(rapidto, xyClearance);
                AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
                break;
        }
    }

    void AddCenterInsideX(Position rapidto, double xyClearance)
    {
        var rapid = _probing.WorkpieceSizeX / 2d - xyClearance;
        if (rapid > 1d)
        {
            rapidto.X -= rapid;
            AddRapidToMPos(rapidto, AxisFlags.X);
            rapidto.X = _probing.StartPosition.X + rapid;
        }

        AddProbingAction(AxisFlags.X, true);
        AddRapidToMPos(rapidto, AxisFlags.X);
        AddProbingAction(AxisFlags.X, false);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.X);
    }

    void AddCenterInsideY(Position rapidto, double xyClearance)
    {
        var rapid = _probing.WorkpieceSizeY / 2d - xyClearance;
        if (rapid > 1d)
        {
            rapidto.Y -= rapid;
            AddRapidToMPos(rapidto, AxisFlags.Y);
            rapidto.Y = _probing.StartPosition.Y + rapid;
        }

        AddProbingAction(AxisFlags.Y, true);
        AddRapidToMPos(rapidto, AxisFlags.Y);
        AddProbingAction(AxisFlags.Y, false);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Y);
    }

    void AddCenterOutsideX(Position rapidto, double xyClearance)
    {
        AddRapidToMPos(rapidto, AxisFlags.X);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(AxisFlags.X, false);
        AddRapidToMPos(rapidto, AxisFlags.X);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        rapidto.X += _probing.WorkpieceSizeX + xyClearance * 2d;
        AddRapidToMPos(rapidto, AxisFlags.X);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(AxisFlags.X, true);
        AddRapidToMPos(rapidto, AxisFlags.X);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.X);
    }

    void AddCenterOutsideY(Position rapidto, double xyClearance)
    {
        AddRapidToMPos(rapidto, AxisFlags.Y);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(AxisFlags.Y, false);
        AddRapidToMPos(rapidto, AxisFlags.Y);
        AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        rapidto.Y += _probing.WorkpieceSizeY + xyClearance * 2d;
        AddRapidToMPos(rapidto, AxisFlags.Y);
        AddRapidToMPos(rapidto, AxisFlags.Z);
        AddProbingAction(AxisFlags.Y, true);
        AddRapidToMPos(rapidto, AxisFlags.Y);
    }

    public void BuildHeightMap(HeightMap map, bool addPause)
    {
        Clear();
        Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));

        var dir = 1d;
        var point = 0;
        var points = map.TotalPoints;

        for (var x = 0; x < map.SizeX; x++)
        {
            for (var y = 0; y < map.SizeY; y++)
            {
                AddMessage(string.Format(ProbingStrings.ProbingPointOf, ++point, points));
                if (addPause && (x > 0 || y > 0))
                    AddPause();
                AddProbingAction(AxisFlags.Z, true);
                AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
                if (y < map.SizeY - 1)
                    AddRapid(string.Format("Y{0}", (map.GridY * dir).ToInvariantString(_probing.Grbl!.Format)));
            }

            if (x < map.SizeX - 1)
                AddRapid(string.Format("X{0}", map.GridX.ToInvariantString(_probing.Grbl!.Format)));

            dir *= -1d;
        }
    }

    public void AppendInternalEdgePostProbePreview(ProbingCycleState state)
    {
        var pos = new Position(_probing.StartPosition);
        foreach (var i in state.AxisFlags.ToIndices())
            pos.Values[i] = _probing.StartPosition.Values[i] +
                (i == GrblConstants.Z_AXIS ? 0d : _probing.ProbeDiameter / 2d * state.Af[i]);

        if (_probing.ProbeZ && state.AxisFlags != AxisFlags.Z)
        {
            var pz = new Position(pos);
            pz.X += _probing.ProbeDiameter / 2d * state.Af[GrblConstants.X_AXIS];
            pz.Y += _probing.ProbeDiameter / 2d * state.Af[GrblConstants.Y_AXIS];
            AddRapidToMPos(pz, state.AxisFlags);
            AddProbingAction(AxisFlags.Z, true);
            AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        }

        AddRapidToMPos(pos, AxisFlags.Y);
        AddRapidToMPos(pos, AxisFlags.X);

        var axisflags = state.AxisFlags;
        if (_probing.ProbeZ)
            axisflags |= AxisFlags.Z;

        if (_probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
        {
            AddRapidToMPos(pos, AxisFlags.Z);
            pos.X = _probing.ProbeOffsetX;
            pos.Y = _probing.ProbeOffsetY;
            pos.Z = _probing.WorkpieceHeight + _probing.TouchPlateHeight;
            Add("G92" + pos.ToString(axisflags));
            if (axisflags.HasFlag(AxisFlags.Z))
                AddRapidToMPos(_probing.StartPosition, AxisFlags.Z);
        }
        else
        {
            pos.X += _probing.ProbeOffsetX;
            pos.Y += _probing.ProbeOffsetY;
            pos.Z -= _probing.WorkpieceHeight + _probing.TouchPlateHeight + _probing.Grbl!.ToolOffset.Z;
            Add(string.Format("G10L2P{0}{1}", _probing.CoordinateSystem, pos.ToString(axisflags)));
        }
    }

    public void AppendCenterFinderPostProbePreview(CenterFindMode mode)
    {
        var axisflags = mode switch
        {
            CenterFindMode.X => AxisFlags.X,
            CenterFindMode.Y => AxisFlags.Y,
            _ => AxisFlags.XY
        };

        AddRapidToMPos(_probing.StartPosition, axisflags);
        if (_probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
        {
            var center = new Position { X = _probing.ProbeOffsetX, Y = _probing.ProbeOffsetY };
            Add("G92" + center.ToString(axisflags));
        }
        else
            Add(string.Format("G10L2P{0}{1}", _probing.CoordinateSystem, _probing.StartPosition.ToString(axisflags)));
    }
}
