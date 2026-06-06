using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class ControllerJogSafetyTests : IDisposable
{
    readonly FakeStreamComms _comms = new();
    readonly double _oldMaxTravelX;
    readonly double _oldMaxTravelY;
    readonly double _oldMaxTravelZ;
    readonly double _oldMaxTravelA;
    readonly List<GrblSettingDetails> _oldSettings;

    public ControllerJogSafetyTests()
    {
        Comms.com = _comms;
        _oldSettings = GrblSettings.Settings.ToList();
        _oldMaxTravelX = GrblInfo.MaxTravel.X;
        _oldMaxTravelY = GrblInfo.MaxTravel.Y;
        _oldMaxTravelZ = GrblInfo.MaxTravel.Z;
        _oldMaxTravelA = GrblInfo.MaxTravel.A;
        GrblInfo.MaxTravel.X = 100d;
        GrblInfo.MaxTravel.Y = 100d;
        GrblInfo.MaxTravel.Z = 100d;
        GrblInfo.MaxTravel.A = 0d;
        SetSoftLimits(enabled: true);
    }

    public void Dispose()
    {
        Comms.com = null;
        UiJogController.IsGrblHALController = () => GrblInfo.IsGrblHAL;
        GrblInfo.MaxTravel.X = _oldMaxTravelX;
        GrblInfo.MaxTravel.Y = _oldMaxTravelY;
        GrblInfo.MaxTravel.Z = _oldMaxTravelZ;
        GrblInfo.MaxTravel.A = _oldMaxTravelA;
        GrblSettings.Settings.Clear();
        foreach (var setting in _oldSettings)
            GrblSettings.Settings.Add(setting);
    }

    [Fact]
    public void Step_jog_clamps_at_max_travel_boundary_when_soft_limits_are_enabled()
    {
        var model = ModelAt("<Idle|MPos:-99.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model, JogViewModel.JogStep.Step2);

        Assert.True(controller.Jog("X-", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G21X-99.5F100", _comms.Commands.Single());
    }

    [Fact]
    public void Step_jog_clamps_at_machine_zero_boundary_when_soft_limits_are_enabled()
    {
        var model = ModelAt("<Idle|MPos:-0.700,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model, JogViewModel.JogStep.Step1);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G21X-0.5F100", _comms.Commands.Single());
    }

    [Fact]
    public void Jog_at_boundary_blocks_only_the_outbound_direction()
    {
        var model = ModelAt("<Idle|MPos:-99.500,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.False(controller.Jog("X-", UiJogCommandMode.Step));
        Assert.Empty(_comms.Commands);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Step));
        Assert.Contains("$J=G53G21X-99.4F100", _comms.Commands.Single());
    }

    [Fact]
    public void Continuous_jog_clamps_positive_direction_to_machine_zero_boundary()
    {
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Continuous));

        Assert.Contains("$J=G53G21X-0.5F100", _comms.Commands.Single());
        Assert.DoesNotContain("G91", _comms.Commands.Single());
    }

    [Fact]
    public void Continuous_jog_clamps_negative_direction_to_max_travel_boundary()
    {
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("X-", UiJogCommandMode.Continuous));

        Assert.Contains("$J=G53G21X-99.5F100", _comms.Commands.Single());
        Assert.DoesNotContain("G91", _comms.Commands.Single());
    }

    [Fact]
    public void Sender_clamped_soft_limit_jog_uses_original_g53_command_shape()
    {
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000|GC:G0 G54 G17 G21 G91 G94 M5 M9 T0 F0 S0|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G21X-49.9F100", _comms.Commands.Single());
    }

    [Fact]
    public void Soft_limit_jog_uses_machine_position_computed_from_wpos_and_wco()
    {
        var model = ModelAt("<Idle|WPos:-51.000,-50.000,-50.000|WCO:1.000,0.000,0.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G21X-49.9F100", _comms.Commands.Single());
    }

    [Fact]
    public void Soft_limit_jog_without_known_machine_position_sends_no_relative_fallback()
    {
        var model = ModelAt("<Idle|WPos:-51.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.False(controller.Jog("X+", UiJogCommandMode.Continuous));

        Assert.Empty(_comms.Commands);
        Assert.NotEmpty(_comms.Bytes);
    }

    [Fact]
    public void Soft_limit_step_without_known_machine_position_sends_no_relative_fallback()
    {
        var model = ModelAt("<Idle|WPos:-51.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.False(controller.Jog("X-", UiJogCommandMode.Step));

        Assert.Empty(_comms.Commands);
        Assert.NotEmpty(_comms.Bytes);
    }

    [Fact]
    public void Imperial_soft_limit_jog_uses_g20_with_absolute_machine_coordinates()
    {
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000|Bf:15,128>");
        model.IsMetric = false;
        var controller = Controller(model);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G20X-49.9F100", _comms.Commands.Single());
    }

    [Fact]
    public void Grblhal_firmware_jog_limiting_keeps_continuous_jog_relative()
    {
        SetFirmwareJogLimiting(enabled: true);
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("X+", UiJogCommandMode.Continuous));

        Assert.Contains("$J=G91G21X100F100", _comms.Commands.Single());
        Assert.DoesNotContain("G53", _comms.Commands.Single());
    }

    [Fact]
    public void Grblhal_firmware_jog_limiting_keeps_step_jog_relative()
    {
        SetFirmwareJogLimiting(enabled: true);
        var model = ModelAt("<Idle|MPos:-99.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model, JogViewModel.JogStep.Step2);

        Assert.True(controller.Jog("X-", UiJogCommandMode.Step));

        Assert.Contains("$J=G91G21X-10F100", _comms.Commands.Single());
        Assert.DoesNotContain("G53", _comms.Commands.Single());
    }

    [Fact]
    public void Grblhal_without_firmware_jog_limiting_uses_sender_boundary_clamp()
    {
        SetFirmwareJogLimiting(enabled: false);
        var model = ModelAt("<Idle|MPos:-99.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model, JogViewModel.JogStep.Step2);

        Assert.True(controller.Jog("X-", UiJogCommandMode.Step));

        Assert.Contains("$J=G53G21X-99.5F100", _comms.Commands.Single());
    }

    [Fact]
    public void Boundary_clamping_is_inactive_when_soft_limits_are_disabled()
    {
        SetSoftLimits(enabled: false);
        var model = ModelAt("<Idle|MPos:-99.000,-50.000,-50.000|Bf:15,128>");
        var controller = Controller(model, JogViewModel.JogStep.Step2);

        Assert.True(controller.Jog("X-", UiJogCommandMode.Step));

        Assert.Contains("$J=G91G21X-10F100", _comms.Commands.Single());
    }

    [Fact]
    public void Axis_with_zero_max_travel_keeps_relative_jogging()
    {
        var model = ModelAt("<Idle|MPos:-50.000,-50.000,-50.000,0.000|Bf:15,128>");
        var controller = Controller(model);

        Assert.True(controller.Jog("A+", UiJogCommandMode.Step));

        Assert.Contains("$J=G91G21A0.1F100", _comms.Commands.Single());
    }

    static GrblViewModel ModelAt(string status)
    {
        var model = new GrblViewModel();
        model.DataReceived(status);
        model.DataReceived(status);
        return model;
    }

    static UiJogController Controller(GrblViewModel model, JogViewModel.JogStep step = JogViewModel.JogStep.Step0)
    {
        var jogData = new JogViewModel();
        jogData.SetMetric(true, new BaseConfig
        {
            JogUiMetric = new JogUIConfig([100, 100, 100, 100], [0.1d, 1d, 10d, 20d])
        });
        jogData.StepSize = step;
        var controller = new UiJogController(jogData, () => null);
        controller.Attach(model);
        jogData.SetMetric(true, new BaseConfig
        {
            JogUiMetric = new JogUIConfig([100, 100, 100, 100], [0.1d, 1d, 10d, 20d])
        });
        jogData.StepSize = step;
        return controller;
    }

    static void SetSoftLimits(bool enabled)
    {
        SetSetting(GrblSetting.SoftLimitsEnable, enabled ? "1" : "0");
        SetSetting(GrblSetting.HomingPulloff, "0.5");
        SetSetting((GrblSetting)grblHALSetting.SoftLimitJogging, "0");
        UiJogController.IsGrblHALController = () => false;
    }

    static void SetFirmwareJogLimiting(bool enabled)
    {
        SetSoftLimits(enabled: true);
        SetSetting((GrblSetting)grblHALSetting.SoftLimitJogging, enabled ? "1" : "0");
        UiJogController.IsGrblHALController = () => true;
    }

    static void SetSetting(GrblSetting key, string value)
    {
        var setting = GrblSettings.Get(key);
        if (setting is null)
        {
            setting = new GrblSettingDetails($"{(int)key}|0||||||");
            GrblSettings.Settings.Add(setting);
        }

        setting.Value = value;
        setting.IsDirty = false;
    }

    sealed class FakeStreamComms : StreamComms
    {
        public List<string> Commands { get; } = new();
        public List<byte> Bytes { get; } = new();
        public bool IsOpen { get; set; } = true;
        public int OutCount => 0;
        public string Reply { get; private set; } = string.Empty;
        public Comms.StreamType StreamType => Comms.StreamType.Serial;
        public Comms.State CommandState { get; set; } = Comms.State.ACK;
        public bool EventMode { get; set; } = true;
        public System.Action<int>? ByteReceived { get; set; }
        public event DataReceivedHandler? DataReceived;

        public void Close() => IsOpen = false;
        public int ReadByte() => -1;
        public void WriteByte(byte data) => Bytes.Add(data);
        public void WriteBytes(byte[] bytes, int len) { }
        public void WriteString(string data) { }
        public void WriteCommand(string command) => Commands.Add(command);
        public string GetReply(string command) => Reply;
        public void AwaitAck() { }
        public void AwaitAck(string command) { }
        public void AwaitResponse(string command) { }
        public void AwaitResponse() { }
        public void PurgeQueue() { }
        public void Raise(string data) => DataReceived?.Invoke(data);
    }
}
