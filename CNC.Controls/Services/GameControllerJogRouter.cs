using CNC.App;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public interface IGameControllerJogExecutor
{
    JogViewModel JogData { get; }

    bool Jog(string command, UiJogCommandMode mode);

    void Stop();
}

public sealed class UiGameControllerJogExecutor : IGameControllerJogExecutor
{
    readonly UiJogController _controller;

    public UiGameControllerJogExecutor(UiJogController controller, JogViewModel jogData)
    {
        _controller = controller;
        JogData = jogData;
    }

    public JogViewModel JogData { get; }

    public bool Jog(string command, UiJogCommandMode mode) =>
        _controller.Jog(command, mode);

    public void Stop() => _controller.Stop();
}

public sealed class GameControllerJogRouter
{
    readonly IGameControllerJogExecutor _executor;
    readonly Func<GameControllerConfig> _config;
    readonly HashSet<GameControllerAction> _heldActions = new();
    bool _distanceModeHeld;
    GameControllerAction? _activeContinuousAction;

    public GameControllerJogRouter(IGameControllerJogExecutor executor, Func<GameControllerConfig> config)
    {
        _executor = executor;
        _config = config;
    }

    public void HandleInput(GameControllerInputKind inputKind, string inputName, bool pressed)
    {
        var binding = _config().Bindings.FirstOrDefault(b =>
            b.InputKind == inputKind &&
            string.Equals(b.InputName, inputName, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
            return;

        HandleAction(binding.Action, pressed);
    }

    public void Tick()
    {
        if (_distanceModeHeld || _activeContinuousAction is not null)
            return;

        foreach (var action in _heldActions)
        {
            var command = JogCommandFor(action);
            if (command is null)
                continue;

            if (_executor.Jog(command, UiJogCommandMode.Continuous))
            {
                _activeContinuousAction = action;
                return;
            }
        }
    }

    public void Cancel()
    {
        _executor.Stop();
        _activeContinuousAction = null;
    }

    void HandleAction(GameControllerAction action, bool pressed)
    {
        if (pressed)
            _heldActions.Add(action);
        else
            _heldActions.Remove(action);

        if (action == GameControllerAction.JogDistanceMode)
        {
            if (pressed && !_distanceModeHeld && _activeContinuousAction is not null)
                Cancel();

            _distanceModeHeld = pressed;
            return;
        }

        if (!pressed)
        {
            if (_activeContinuousAction == action)
            {
                Cancel();
            }

            return;
        }

        switch (action)
        {
            case GameControllerAction.JogCancel:
                Cancel();
                return;
            case GameControllerAction.CycleJogDistance:
                _executor.JogData.CycleStepSkippingContinuous();
                return;
            case GameControllerAction.CycleJogFeedRate:
                _executor.JogData.CycleFeed();
                return;
        }

        var command = JogCommandFor(action);
        if (command is null)
            return;

        if (_distanceModeHeld)
        {
            _executor.Jog(command, UiJogCommandMode.Step);
            return;
        }

        if (_activeContinuousAction is not null)
            return;

        if (_executor.Jog(command, UiJogCommandMode.Continuous))
            _activeContinuousAction = action;
    }

    static string? JogCommandFor(GameControllerAction action)
    {
        if (GrblInfo.LatheModeEnabled)
        {
            return action switch
            {
                GameControllerAction.JogXPlus => "Z+",
                GameControllerAction.JogXMinus => "Z-",
                GameControllerAction.JogYPlus => "X-",
                GameControllerAction.JogYMinus => "X+",
                _ => null
            };
        }

        return action switch
        {
            GameControllerAction.JogYPlus => "Y+",
            GameControllerAction.JogYMinus => "Y-",
            GameControllerAction.JogXMinus => "X-",
            GameControllerAction.JogXPlus => "X+",
            GameControllerAction.JogZPlus => "Z+",
            GameControllerAction.JogZMinus => "Z-",
            _ => null
        };
    }
}
