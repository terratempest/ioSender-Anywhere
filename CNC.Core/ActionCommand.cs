namespace CNC.Core;

public sealed class ActionCommand<TParameter>
{
    public event EventHandler? CanExecuteChanged;

    private readonly System.Action<TParameter>? _execute;
    private readonly Func<TParameter, bool>? _canExecute;

    public ActionCommand(System.Action<TParameter> executeMethod) => _execute = executeMethod;

    public ActionCommand(System.Action<TParameter> executeMethod, Func<TParameter, bool> canExecuteMethod)
        : this(executeMethod) => _canExecute = canExecuteMethod;

    public bool CanExecute(TParameter parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(TParameter parameter) => _execute?.Invoke(parameter);

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class ActionCommand
{
    public event EventHandler? CanExecuteChanged;

    private readonly System.Action? _execute;
    private readonly Func<bool>? _canExecute;

    public ActionCommand(System.Action executeMethod) => _execute = executeMethod;

    public ActionCommand(System.Action executeMethod, Func<bool> canExecuteMethod)
        : this(executeMethod) => _canExecute = canExecuteMethod;

    public bool CanExecute() => _canExecute?.Invoke() ?? true;

    public void Execute() => _execute?.Invoke();

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
