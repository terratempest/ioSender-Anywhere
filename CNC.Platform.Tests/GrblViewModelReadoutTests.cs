using System.ComponentModel;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;
using CNC.GCodeViewer.Avalonia;

namespace CNC.Platform.Tests;

public sealed class GrblViewModelReadoutTests
{
    [Fact]
    public void DataReceived_status_report_updates_position_and_notifies()
    {
        var vm = new GrblViewModel();
        var positionChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrblViewModel.Position))
                positionChanges++;
        };

        vm.DataReceived("<Idle|MPos:10.000,20.000,30.000|WCO:1.000,2.000,3.000|Bf:15,128>");

        Assert.Equal(9d, vm.Position.X, 3);
        Assert.Equal(18d, vm.Position.Y, 3);
        Assert.Equal(27d, vm.Position.Z, 3);
        Assert.True(positionChanges > 0);
        Assert.NotEqual(GrblStates.Unknown, vm.GrblState.State);
    }

    [Fact]
    public void DataReceived_mpos_without_wco_updates_dro_position()
    {
        var vm = new GrblViewModel();
        var positionChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrblViewModel.Position))
                positionChanges++;
        };

        vm.DataReceived("<Idle|MPos:1.000,2.000,3.000,4.000|Bf:100,1023|FS:0,0>");

        Assert.Equal(1d, vm.Position.X, 3);
        Assert.Equal(2d, vm.Position.Y, 3);
        Assert.Equal(3d, vm.Position.Z, 3);
        Assert.True(positionChanges > 0);
    }

    [Fact]
    public void DataReceived_status_report_sets_grbl_state_idle()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|Bf:100,1023>");
        Assert.Equal(GrblStates.Idle, vm.GrblState.State);
        Assert.Equal("Idle", vm.GrblStateDisplay);
    }

    [Fact]
    public void DataReceived_parser_state_updates_work_coordinate_system()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 M5 M9 T0 F0 S0]");
        Assert.Equal("G54", vm.WorkCoordinateSystem);

        vm.DataReceived("[GC:G0 G59.2 G17 G21 G90 G94 M5 M9 T0 F0 S0]");
        Assert.Equal("G59.2", vm.WorkCoordinateSystem);
    }

    [Fact]
    public void DataReceived_parser_state_updates_tool_offset_active_state()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 G43.1 M5 M9 T0 F0 S0]");

        Assert.True(vm.IsToolOffsetActive);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);

        changed.Clear();

        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 G49 M5 M9 T0 F0 S0]");

        Assert.False(vm.IsToolOffsetActive);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);
    }

    [Fact]
    public void DataReceived_grbl_banner_then_status_restores_idle_display()
    {
        var vm = new GrblViewModel { IsReady = true };
        vm.DataReceived("<Idle|MPos:0,0,0|Bf:100,1023>");
        vm.DataReceived("Grbl 1.1h ['$' for help]");
        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        vm.DataReceived("<Idle|MPos:0,0,0|Bf:100,1023>");
        Assert.Equal(GrblStates.Idle, vm.GrblState.State);
        Assert.Equal("Idle", vm.GrblStateDisplay);
    }

    [Fact]
    public void DataReceived_startup_banner_clears_homed_state_to_unknown()
    {
        var vm = new GrblViewModel { IsReady = true };
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.DataReceived("Grbl 1.1h ['$' for help]");

        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        Assert.Equal(HomedState.Unknown, vm.HomedState);
    }

    [Fact]
    public void DataReceived_status_report_without_fields_updates_state()
    {
        var vm = new GrblViewModel();
        var stateChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrblViewModel.GrblState))
                stateChanges++;
        };

        vm.DataReceived("<Idle>");

        Assert.Equal(GrblStates.Idle, vm.GrblState.State);
        Assert.True(stateChanges > 0);
    }

    [Fact]
    public void DataReceived_startup_banner_notifies_unknown_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle>");
        var stateChanges = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GrblViewModel.GrblState))
                stateChanges++;
        };

        vm.DataReceived("Grbl 1.1h ['$' for help]");

        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        Assert.True(stateChanges > 0);
    }

    [Fact]
    public void DataReceived_status_report_notifies_position_axis_properties()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("<Idle|MPos:10.000,20.000,30.000|WCO:1.000,2.000,3.000|Bf:15,128>");

        Assert.Contains("Position.X", changed);
        Assert.Contains("Position.Y", changed);
        Assert.Contains("Position.Z", changed);
        Assert.Contains(nameof(GrblViewModel.Position), changed);
        Assert.True(vm.IsDroPositionKnown);
    }

    [Fact]
    public void DataReceived_run_status_updates_tool_position_from_machine_position_and_wco()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Run|MPos:10.000,20.000,30.000|WCO:1.000,2.000,3.000|Ln:10|Bf:15,128>");

        var toolPosition = ViewerToolMarker.GetToolPosition(vm);
        Assert.Equal(9d, toolPosition.X, 3);
        Assert.Equal(18d, toolPosition.Y, 3);
        Assert.Equal(27d, toolPosition.Z, 3);
    }

    [Fact]
    public void BuildCone_splits_sides_and_base_faces()
    {
        var cone = ViewerToolMarker.BuildCone(new Point3D(), toolDiameter: 1d);

        Assert.Equal(72, cone.Sides.Count);
        Assert.Equal(72, cone.Base.Count);
        Assert.All(cone.Base, point => Assert.Equal(54f, point.Z));
    }

    [Fact]
    public void Build_crosshair_geometry_is_unchanged()
    {
        var bounds = new PathBounds
        {
            MinX = 0,
            MinY = 0,
            MinZ = 0,
            MaxX = 10,
            MaxY = 10,
            MaxZ = 10,
            HasValue = true,
        };

        var points = ViewerToolMarker.Build(bounds, new Point3D(), ToolVisualizerMode.Crosshair, toolDiameter: 1d);

        Assert.Equal(6, points.Count);
    }

    [Fact]
    public void DataReceived_line_number_advances_execution_progress_without_streaming_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Run|MPos:0,0,0|WCO:0,0,0|Ln:10|Bf:15,128>");
        vm.DataReceived("<Run|MPos:1,0,0|WCO:0,0,0|Ln:10|Bf:15,128>");

        Assert.True(vm.ExecutionProgress.HasLineNumberReports);
        Assert.Equal(10u, vm.ExecutionProgress.CurrentLineNumber);
        Assert.Empty(vm.ExecutionProgress.CompletedLineNumbers);

        vm.DataReceived("<Run|MPos:2,0,0|WCO:0,0,0|Ln:20|Bf:15,128>");

        Assert.Equal(20u, vm.ExecutionProgress.CurrentLineNumber);
        Assert.Contains(10u, vm.ExecutionProgress.CompletedLineNumbers);
    }

    [Fact]
    public void DataReceived_status_report_notifies_machine_position_axes()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("<Idle|MPos:1.000,2.000,3.000|Bf:100,1023>");

        Assert.Contains("MachinePosition.X", changed);
        Assert.Contains(nameof(GrblViewModel.MachinePosition), changed);
        Assert.True(vm.IsMachinePositionKnown);
    }

    [Fact]
    public void Position_suspend_notifications_resume_refreshes_axis_and_aggregate()
    {
        var position = new Position();
        var changed = new List<string>();
        position.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        position.SuspendNotifications = true;
        position.X = 42d;
        changed.Clear();

        position.SuspendNotifications = false;

        Assert.Contains("X", changed);
        Assert.Contains(nameof(Position), changed);
        Assert.Equal(42d, position.X);
    }

    [Fact]
    public void DataReceived_idle_without_position_is_not_dro_known()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle>");
        Assert.False(vm.IsDroPositionKnown);
    }

    [Fact]
    public void DataReceived_pin_field_updates_signals_and_notifies()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("<Idle|MPos:0,0,0|Pn:X|Bf:15,128>");

        Assert.True(vm.Signals.Value.HasFlag(Signals.LimitX));
        Assert.Contains(nameof(GrblViewModel.Signals), changed);
        Assert.Contains("Signals.Item[]", changed);
    }

    [Fact]
    public void DataReceived_accessory_field_updates_spindle_state()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("<Idle|MPos:0,0,0|A:S|Bf:15,128>");

        Assert.True(vm.SpindleState.Value.HasFlag(SpindleState.CW));
        Assert.Contains(nameof(GrblViewModel.SpindleState), changed);
        Assert.Contains("SpindleState.Item[]", changed);
    }

    [Fact]
    public void AxisScaled_flag_holder_supports_has_flag_checks_used_by_dro()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|Sc:X|Bf:15,128>");

        Assert.True(vm.AxisScaled.Value.HasFlag(AxisFlags.X));
        Assert.False(vm.AxisScaled.Value.HasFlag(AxisFlags.Y));
    }

    [Fact]
    public void DataReceived_homed_field_sets_homed_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        Assert.Equal(HomedState.Homed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_not_homed_field_sets_unknown_state_when_not_alarm_11()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Idle|MPos:0,0,0|H:0|Bf:15,128>");

        Assert.Equal(HomedState.Unknown, vm.HomedState);
    }

    [Fact]
    public void DataReceived_alarm_11_sets_not_homed_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Alarm:11|MPos:0,0,0|Bf:15,128>");

        Assert.Equal(HomedState.NotHomed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_alarm_11_homed_field_sets_not_homed_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Alarm:11|MPos:0,0,0|H:0|Bf:15,128>");

        Assert.Equal(GrblStates.Alarm, vm.GrblState.State);
        Assert.Equal(11, vm.GrblState.Substate);
        Assert.Equal(HomedState.NotHomed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_home_report_sets_homed_when_all_required_axes_are_homed()
    {
        var oldHomingAxes = GrblInfo.HomingAxes;
        var oldAxisFlags = GrblInfo.AxisFlags;

        try
        {
            GrblInfo.HomingAxes = AxisFlags.XYZ;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), AxisFlags.XYZ);
            var vm = new GrblViewModel();

            vm.DataReceived("[HOME:0,0,0:7]");

            Assert.Equal(AxisFlags.XYZ, vm.AxisHomed.Value);
            Assert.Equal(HomedState.Homed, vm.HomedState);
        }
        finally
        {
            GrblInfo.HomingAxes = oldHomingAxes;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), oldAxisFlags);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void DataReceived_home_report_sets_not_homed_when_required_axes_are_missing(int mask)
    {
        var oldHomingAxes = GrblInfo.HomingAxes;
        var oldAxisFlags = GrblInfo.AxisFlags;

        try
        {
            GrblInfo.HomingAxes = AxisFlags.XYZ;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), AxisFlags.XYZ);
            var vm = new GrblViewModel();

            vm.DataReceived($"[HOME:0,0,0:{mask}]");

            Assert.Equal((AxisFlags)mask, vm.AxisHomed.Value);
            Assert.Equal(HomedState.NotHomed, vm.HomedState);
        }
        finally
        {
            GrblInfo.HomingAxes = oldHomingAxes;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), oldAxisFlags);
        }
    }

    [Fact]
    public void DataReceived_home_report_sets_unknown_when_no_expected_axes_are_available()
    {
        var oldHomingAxes = GrblInfo.HomingAxes;
        var oldAxisFlags = GrblInfo.AxisFlags;

        try
        {
            GrblInfo.HomingAxes = AxisFlags.None;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), AxisFlags.None);
            var vm = new GrblViewModel();

            vm.DataReceived("[HOME:0,0,0:7]");

            Assert.Equal(HomedState.Unknown, vm.HomedState);
        }
        finally
        {
            GrblInfo.HomingAxes = oldHomingAxes;
            SetStaticProperty(nameof(GrblInfo.AxisFlags), oldAxisFlags);
        }
    }

    [Fact]
    public void DataReceived_hard_limit_alarm_keeps_existing_homed_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.DataReceived("ALARM:1");

        Assert.Equal(GrblStates.Alarm, vm.GrblState.State);
        Assert.Equal(1, vm.GrblState.Substate);
        Assert.Equal(HomedState.Homed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_soft_limit_alarm_keeps_existing_homed_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.DataReceived("ALARM:2");

        Assert.Equal(GrblStates.Alarm, vm.GrblState.State);
        Assert.Equal(2, vm.GrblState.Substate);
        Assert.Equal(HomedState.Homed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_estop_alarm_keeps_existing_homed_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.DataReceived("ALARM:10");

        Assert.Equal(GrblStates.Alarm, vm.GrblState.State);
        Assert.Equal(10, vm.GrblState.Substate);
        Assert.Equal(HomedState.Homed, vm.HomedState);
    }

    [Fact]
    public void Clear_resets_homed_state_to_unknown()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.Clear();

        Assert.Equal(HomedState.Unknown, vm.HomedState);
    }

    [Fact]
    public void ClearPosition_keeps_existing_homed_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.ClearPosition();

        Assert.Equal(HomedState.Homed, vm.HomedState);
    }

    [Fact]
    public void DataReceived_tlo_report_activates_tool_offset_indicator()
    {
        var vm = new GrblViewModel();
        Assert.False(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);

        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[TLO:0.000,0.000,12.345]");

        Assert.Equal(12.345d, vm.ToolOffset.Z, 3);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void DataReceived_tlo_zero_report_clears_tool_offset_indicator()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("[TLO:0.000,0.000,12.345]");

        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[TLO:0.000,0.000,0.000]");

        Assert.Equal(0d, vm.ToolOffset.Z, 3);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void Tlo_report_overrides_stale_parser_cancel_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 G49 M5 M9 T0 F0 S0]");

        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[TLO:0.000,0.000,12.345]");

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void Tlo_zero_report_overrides_stale_parser_active_state()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 G43.1 M5 M9 T0 F0 S0]");

        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[TLO:0.000,0.000,0.000]");

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void Parser_active_state_overrides_stale_numeric_zero_report()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("[TLO:0.000,0.000,0.000]");

        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.DataReceived("[GC:G0 G54 G17 G21 G90 G94 G43.1 M5 M9 T0 F0 S0]");

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void Realtime_parser_state_updates_tool_offset_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Idle|MPos:0,0,0|GC:G0 G54 G17 G21 G90 G94 G43.1 M5 M9 T0 F0 S0|Bf:15,128>");

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);

        vm.DataReceived("<Idle|MPos:0,0,0|GC:G0 G54 G17 G21 G90 G94 G49 M5 M9 T0 F0 S0|Bf:15,128>");

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);
    }

    [Fact]
    public void Realtime_tlo_state_updates_tool_offset_state()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Idle|MPos:0,0,0|TLO:0.000,0.000,12.345|Bf:15,128>");

        Assert.Equal(12.345d, vm.ToolOffset.Z, 3);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);

        vm.DataReceived("<Idle|MPos:0,0,0|TLO:0.000,0.000,0.000|Bf:15,128>");

        Assert.Equal(0d, vm.ToolOffset.Z, 3);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);
    }

    [Fact]
    public void Realtime_tlo_state_overrides_parser_cancel_in_same_report()
    {
        var vm = new GrblViewModel();

        vm.DataReceived("<Idle|MPos:0,0,0|GC:G0 G54 G17 G21 G90 G94 G49 M5 M9 T0 F0 S0|TLR:1|TLO:0.000,0.000,12.345|Bf:15,128>");

        Assert.True(vm.IsTloReferenceSet);
        Assert.Equal(12.345d, vm.ToolOffset.Z, 3);
        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);
    }

    [Fact]
    public void ToolOffset_z_change_updates_tool_offset_state()
    {
        var vm = new GrblViewModel();
        var changed = new HashSet<string>(StringComparer.Ordinal);
        vm.PropertyChanged += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changed.Add(e.PropertyName);
        };

        vm.ToolOffset.Z = 4.5d;

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.True(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetIndicatorVisible), changed);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);

        changed.Clear();
        vm.ToolOffset.Z = 0d;

        Assert.True(vm.IsToolOffsetIndicatorVisible);
        Assert.False(vm.IsToolOffsetActive);
        Assert.Contains(nameof(GrblViewModel.IsToolOffsetActive), changed);
    }

    [Fact]
    public void Parser_state_offset_detection_treats_non_zero_position_as_offset()
    {
        var method = typeof(GrblParserState).GetMethod(
            "IsPositionOffset",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        Assert.False((bool)method.Invoke(null, [new Position(0d, 0d, 0d)])!);
        Assert.True((bool)method.Invoke(null, [new Position(0d, 0d, 12.345d)])!);
    }

    [Fact]
    public void Spindle_override_does_not_shrink_setpoint_while_fs_lags()
    {
        var vm = new GrblViewModel();
        const string running = "<Idle|MPos:0,0,0|A:S|FS:0,10000|Ov:100,100,{0}|Bf:15,128>";

        vm.DataReceived(string.Format(running, 100));
        Assert.Equal(10000, vm.SpindleSetpointRPM);
        Assert.Equal(10000, vm.RPM);

        vm.DataReceived(string.Format(running, 110));
        Assert.Equal(10000, vm.SpindleSetpointRPM);
        Assert.Equal(11000, vm.RPM);

        vm.DataReceived(string.Format(running, 120));
        Assert.Equal(10000, vm.SpindleSetpointRPM);
        Assert.Equal(12000, vm.RPM);
    }

    [Fact]
    public void Spindle_fs_effective_rpm_tracks_setpoint_when_override_matches()
    {
        var vm = new GrblViewModel();
        vm.DataReceived("<Idle|MPos:0,0,0|A:S|FS:0,10000|Ov:100,100,100|Bf:15,128>");
        Assert.Equal(10000, vm.SpindleSetpointRPM);

        vm.DataReceived("<Idle|MPos:0,0,0|A:S|FS:0,11000|Ov:100,100,110|Bf:15,128>");
        Assert.Equal(10000, vm.SpindleSetpointRPM);
        Assert.Equal(11000, vm.RPM);
    }

    static void SetStaticProperty<T>(string name, T value)
    {
        typeof(GrblInfo)
            .GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!
            .SetValue(null, value);
    }
}
