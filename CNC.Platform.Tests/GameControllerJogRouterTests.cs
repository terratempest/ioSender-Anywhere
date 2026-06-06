using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;

namespace CNC.Platform.Tests;

public sealed class GameControllerJogRouterTests
{
    [Fact]
    public void Direction_press_starts_continuous_and_release_cancels()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Button, "DPadUp", true);
        router.HandleInput(GameControllerInputKind.Button, "DPadUp", false);

        Assert.Equal(("Y+", UiJogCommandMode.Continuous), executor.Jogs.Single());
        Assert.Equal(1, executor.StopCount);
    }

    [Fact]
    public void Start_button_cancels_active_jog()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Button, "DPadRight", true);
        router.HandleInput(GameControllerInputKind.Button, "Start", true);

        Assert.Equal(("X+", UiJogCommandMode.Continuous), executor.Jogs.Single());
        Assert.Equal(1, executor.StopCount);
    }

    [Fact]
    public void L2_turns_direction_press_into_single_step_jog()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Axis, "LeftTrigger", true);
        router.HandleInput(GameControllerInputKind.Button, "South", true);
        router.HandleInput(GameControllerInputKind.Button, "South", false);

        Assert.Equal(("Z-", UiJogCommandMode.Step), executor.Jogs.Single());
        Assert.Equal(0, executor.StopCount);
    }

    [Fact]
    public void Pressing_L2_while_continuous_jogging_cancels_before_step_mode()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Button, "DPadLeft", true);
        router.HandleInput(GameControllerInputKind.Axis, "LeftTrigger", true);
        router.HandleInput(GameControllerInputKind.Button, "East", true);

        Assert.Equal(new[]
        {
            ("X-", UiJogCommandMode.Continuous),
            ("Z+", UiJogCommandMode.Step)
        }, executor.Jogs);
        Assert.Equal(1, executor.StopCount);
    }

    [Fact]
    public void Second_direction_is_ignored_while_continuous_jog_is_active()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Button, "DPadUp", true);
        router.HandleInput(GameControllerInputKind.Button, "DPadRight", true);
        router.HandleInput(GameControllerInputKind.Button, "DPadRight", false);
        router.HandleInput(GameControllerInputKind.Button, "DPadUp", false);

        Assert.Equal(("Y+", UiJogCommandMode.Continuous), executor.Jogs.Single());
        Assert.Equal(1, executor.StopCount);
    }

    [Fact]
    public void L1_cycles_jog_distance_without_selecting_continuous()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);
        executor.JogData.StepSize = JogViewModel.JogStep.Step3;

        router.HandleInput(GameControllerInputKind.Button, "LeftShoulder", true);
        router.HandleInput(GameControllerInputKind.Button, "LeftShoulder", false);
        Assert.Equal(JogViewModel.JogStep.Step0, executor.JogData.StepSize);

        executor.JogData.StepSize = JogViewModel.JogStep.Continuous;
        router.HandleInput(GameControllerInputKind.Button, "LeftShoulder", true);

        Assert.Equal(JogViewModel.JogStep.Step0, executor.JogData.StepSize);
    }

    [Fact]
    public void R1_cycles_jog_feed_rates_and_wraps()
    {
        var executor = new FakeJogExecutor();
        var router = Router(executor);
        executor.JogData.Feed = JogViewModel.JogFeed.Feed3;

        router.HandleInput(GameControllerInputKind.Button, "RightShoulder", true);

        Assert.Equal(JogViewModel.JogFeed.Feed0, executor.JogData.Feed);
    }

    [Fact]
    public void Held_direction_retries_continuous_jog_on_tick_after_initial_start_fails()
    {
        var executor = new FakeJogExecutor { NextJogResult = false };
        var router = Router(executor);

        router.HandleInput(GameControllerInputKind.Button, "DPadUp", true);
        executor.NextJogResult = true;
        router.Tick();

        Assert.Equal(new[]
        {
            ("Y+", UiJogCommandMode.Continuous),
            ("Y+", UiJogCommandMode.Continuous)
        }, executor.Jogs);
    }

    static GameControllerJogRouter Router(FakeJogExecutor executor)
    {
        var config = new GameControllerConfig();
        return new GameControllerJogRouter(executor, () => config);
    }

    sealed class FakeJogExecutor : IGameControllerJogExecutor
    {
        public FakeJogExecutor()
        {
            JogData.SetMetric(true);
        }

        public JogViewModel JogData { get; } = new();
        public List<(string Command, UiJogCommandMode Mode)> Jogs { get; } = new();
        public int StopCount { get; private set; }
        public bool NextJogResult { get; set; } = true;

        public bool Jog(string command, UiJogCommandMode mode)
        {
            Jogs.Add((command, mode));
            return NextJogResult;
        }

        public void Stop() => StopCount++;
    }
}
