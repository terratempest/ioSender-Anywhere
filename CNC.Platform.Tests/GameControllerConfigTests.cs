using System.Xml.Serialization;
using CNC.App;

namespace CNC.Platform.Tests;

public sealed class GameControllerConfigTests
{
    [Fact]
    public void Default_bindings_match_requested_controller_layout()
    {
        var config = new BaseConfig();

        AssertBinding(config, GameControllerAction.JogYPlus, GameControllerInputKind.Button, "DPadUp");
        AssertBinding(config, GameControllerAction.JogYMinus, GameControllerInputKind.Button, "DPadDown");
        AssertBinding(config, GameControllerAction.JogXMinus, GameControllerInputKind.Button, "DPadLeft");
        AssertBinding(config, GameControllerAction.JogXPlus, GameControllerInputKind.Button, "DPadRight");
        AssertBinding(config, GameControllerAction.JogZPlus, GameControllerInputKind.Button, "East");
        AssertBinding(config, GameControllerAction.JogZMinus, GameControllerInputKind.Button, "South");
        AssertBinding(config, GameControllerAction.CycleJogDistance, GameControllerInputKind.Button, "LeftShoulder");
        AssertBinding(config, GameControllerAction.CycleJogFeedRate, GameControllerInputKind.Button, "RightShoulder");
        AssertBinding(config, GameControllerAction.JogDistanceMode, GameControllerInputKind.Axis, "LeftTrigger");
        AssertBinding(config, GameControllerAction.JogCancel, GameControllerInputKind.Button, "Start");
    }

    [Fact]
    public void Game_controller_config_round_trips_through_base_config_xml()
    {
        var config = new BaseConfig();
        config.GameController.Enabled = true;
        config.GameController.PreferredControllerName = "Test Controller";
        config.GameController.Bindings
            .First(binding => binding.Action == GameControllerAction.JogZPlus)
            .InputName = "North";

        var serializer = new XmlSerializer(typeof(BaseConfig));
        using var writer = new StringWriter();
        serializer.Serialize(writer, config);

        using var reader = new StringReader(writer.ToString());
        var loaded = (BaseConfig)serializer.Deserialize(reader)!;
        loaded.GameController.EnsureDefaultBindings();

        Assert.True(loaded.GameController.Enabled);
        Assert.Equal("Test Controller", loaded.GameController.PreferredControllerName);
        AssertBinding(loaded, GameControllerAction.JogZPlus, GameControllerInputKind.Button, "North");
    }

    [Fact]
    public void Reset_bindings_restores_defaults()
    {
        var config = new GameControllerConfig();
        config.Bindings.First(binding => binding.Action == GameControllerAction.JogCancel).InputName = "Back";

        config.ResetBindings();

        Assert.Equal("Start", config.Bindings.First(binding => binding.Action == GameControllerAction.JogCancel).InputName);
    }

    static void AssertBinding(BaseConfig config, GameControllerAction action, GameControllerInputKind kind, string inputName)
    {
        var binding = config.GameController.Bindings.Single(binding => binding.Action == action);
        Assert.Equal(kind, binding.InputKind);
        Assert.Equal(inputName, binding.InputName);
    }
}
