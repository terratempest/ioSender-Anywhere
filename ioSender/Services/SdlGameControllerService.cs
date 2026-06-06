using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using SDL3;

namespace ioSender.Services;

public sealed class SdlGameControllerService : IGameControllerBindingCapture, IDisposable
{
    static readonly SDL.GamepadButton[] Buttons = Enum.GetValues<SDL.GamepadButton>()
        .Where(button => button is not SDL.GamepadButton.Invalid and not SDL.GamepadButton.Count)
        .ToArray();
    static readonly SDL.GamepadAxis[] Axes = Enum.GetValues<SDL.GamepadAxis>()
        .Where(axis => axis is not SDL.GamepadAxis.Invalid and not SDL.GamepadAxis.Count)
        .ToArray();

    readonly AppConfigService _appConfig;
    readonly PlatformServices _platform;
    readonly UiJogController _jogController;
    readonly GameControllerJogRouter _router;
    readonly Dictionary<string, bool> _buttonStates = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, bool> _axisStates = new(StringComparer.OrdinalIgnoreCase);
    readonly object _captureLock = new();
    CancellationTokenSource? _loopCancellation;
    Task? _loopTask;
    TaskCompletionSource<GameControllerBinding?>? _capture;
    IntPtr _gamepad;
    uint _gamepadId;
    string _statusText = "Controller runtime is stopped.";

    public SdlGameControllerService(
        AppConfigService appConfig,
        PlatformServices platform,
        GrblViewModel model)
    {
        _appConfig = appConfig;
        _platform = platform;
        _jogController = new UiJogController(JogViewModel.Shared, () => _appConfig);
        _jogController.Attach(model);
        _router = new GameControllerJogRouter(
            new UiGameControllerJogExecutor(_jogController, JogViewModel.Shared),
            () => _appConfig.Base.GameController);
    }

    public string StatusText => _statusText;

    public bool IsApplicationActive { get; set; } = true;

    public void Cancel() => _platform.UiDispatcher.Post(_router.Cancel);

    public void Start()
    {
        if (_loopTask is not null)
            return;

        _loopCancellation = new CancellationTokenSource();
        _loopTask = Task.Run(() => Run(_loopCancellation.Token));
    }

    public async Task<GameControllerBinding?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<GameControllerBinding?> capture;
        lock (_captureLock)
        {
            if (_capture is not null)
                return null;

            capture = new TaskCompletionSource<GameControllerBinding?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _capture = capture;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var registration = timeout.Token.Register(_ =>
        {
            lock (_captureLock)
            {
                _capture?.TrySetResult(null);
                _capture = null;
            }
        }, null);

        return await capture.Task.ConfigureAwait(false);
    }

    void Run(CancellationToken cancellationToken)
    {
        try
        {
            SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "0");
            if (!SDL.Init(SDL.InitFlags.Gamepad))
            {
                _statusText = "Controller runtime failed to initialize: " + SDL.GetError();
                return;
            }

            _statusText = "No controller connected.";

            while (!cancellationToken.IsCancellationRequested)
            {
                DrainEvents();
                EnsureGamepad();
                PollGamepad();
                _platform.UiDispatcher.Post(() =>
                {
                    if (_appConfig.Base.GameController.Enabled && IsApplicationActive)
                        _router.Tick();
                });
                Thread.Sleep(20);
            }
        }
        catch (Exception ex)
        {
            _statusText = "Controller runtime error: " + ex.Message;
        }
        finally
        {
            CloseGamepad();
            SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
        }
    }

    static void DrainEvents()
    {
        while (SDL.PollEvent(out var _))
        {
        }
    }

    void EnsureGamepad()
    {
        if (_gamepad != IntPtr.Zero && SDL.GamepadConnected(_gamepad))
            return;

        CloseGamepad();

        var ids = SDL.GetGamepads(out var count);
        if (count <= 0 || ids is null || ids.Length == 0)
        {
            _statusText = "No controller connected.";
            return;
        }

        var preferred = _appConfig.Base.GameController.PreferredControllerName;
        var selected = ids[0];
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            foreach (var id in ids)
            {
                var name = SDL.GetGamepadNameForID(id);
                if (string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase))
                {
                    selected = id;
                    break;
                }
            }
        }

        _gamepad = SDL.OpenGamepad(selected);
        if (_gamepad == IntPtr.Zero)
        {
            _statusText = "Controller open failed: " + SDL.GetError();
            return;
        }

        _gamepadId = selected;
        var controllerName = SDL.GetGamepadName(_gamepad) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_appConfig.Base.GameController.PreferredControllerName))
            _appConfig.Base.GameController.PreferredControllerName = controllerName;
        _statusText = string.IsNullOrWhiteSpace(controllerName)
            ? "Controller connected."
            : $"Controller connected: {controllerName}";
        _buttonStates.Clear();
        _axisStates.Clear();
    }

    void CloseGamepad()
    {
        if (_gamepad == IntPtr.Zero)
            return;

        SDL.CloseGamepad(_gamepad);
        _gamepad = IntPtr.Zero;
        _gamepadId = 0;
        _buttonStates.Clear();
        _axisStates.Clear();
        _router.Cancel();
    }

    void PollGamepad()
    {
        if (_gamepad == IntPtr.Zero)
            return;

        SDL.UpdateGamepads();

        foreach (var button in Buttons)
        {
            var name = button.ToString();
            var pressed = SDL.GetGamepadButton(_gamepad, button);
            if (!SetState(_buttonStates, name, pressed))
                continue;

            Dispatch(GameControllerInputKind.Button, name, pressed);
        }

        foreach (var axis in Axes)
        {
            var name = axis.ToString();
            var threshold = ThresholdForAxis(name);
            var pressed = SDL.GetGamepadAxis(_gamepad, axis) >= threshold;
            if (!SetState(_axisStates, name, pressed))
                continue;

            Dispatch(GameControllerInputKind.Axis, name, pressed);
        }
    }

    int ThresholdForAxis(string axisName)
    {
        var binding = _appConfig.Base.GameController.Bindings.FirstOrDefault(b =>
            b.InputKind == GameControllerInputKind.Axis &&
            string.Equals(b.InputName, axisName, StringComparison.OrdinalIgnoreCase));
        return binding?.Threshold > 0 ? binding.Threshold : 16000;
    }

    static bool SetState(Dictionary<string, bool> states, string name, bool pressed)
    {
        if (states.TryGetValue(name, out var old) && old == pressed)
            return false;

        states[name] = pressed;
        return true;
    }

    void Dispatch(GameControllerInputKind inputKind, string inputName, bool pressed)
    {
        if (pressed && CompleteCapture(inputKind, inputName))
            return;

        if (!_appConfig.Base.GameController.Enabled || !IsApplicationActive)
            return;

        _platform.UiDispatcher.Post(() =>
        {
            if (_appConfig.Base.GameController.Enabled && IsApplicationActive)
                _router.HandleInput(inputKind, inputName, pressed);
        });
    }

    bool CompleteCapture(GameControllerInputKind inputKind, string inputName)
    {
        lock (_captureLock)
        {
            if (_capture is null)
                return false;

            _capture.TrySetResult(new GameControllerBinding
            {
                InputKind = inputKind,
                InputName = inputName,
                Threshold = inputKind == GameControllerInputKind.Axis ? 16000 : 0
            });
            _capture = null;
            return true;
        }
    }

    public void Dispose()
    {
        _loopCancellation?.Cancel();
        try
        {
            _loopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        _loopCancellation?.Dispose();
        _jogController.Dispose();
    }
}
