namespace CNC.Core.Input;

public interface IKeyHandlerContext
{
    string Name { get; }
    object? DataContext { get; }
}
