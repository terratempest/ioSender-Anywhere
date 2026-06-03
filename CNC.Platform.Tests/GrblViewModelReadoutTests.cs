using System.ComponentModel;
using CNC.Core;
using CNC.GCode;

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
    public void DataReceived_startup_banner_keeps_homed_state()
    {
        var vm = new GrblViewModel { IsReady = true };
        vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");

        vm.DataReceived("Grbl 1.1h ['$' for help]");

        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        Assert.Equal(HomedState.Homed, vm.HomedState);
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
}
