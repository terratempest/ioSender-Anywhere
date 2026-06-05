using Avalonia.Controls;

namespace CNC.Controls.DragKnife;

public interface IGCodeTransformer
{
    void Apply(Window? owner);
}
