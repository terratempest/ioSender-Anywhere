using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ioSender.Workspace.Controls;

public sealed class WorkspaceGridSplitter : GridSplitter
{
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(Brushes.Transparent, new Rect(bounds.Size));

        if (Background is not { } brush)
            return;

        var line = ResizeDirection == GridResizeDirection.Columns
            ? new Rect(Math.Floor((bounds.Width - 1) / 2), 0, 1, bounds.Height)
            : new Rect(0, Math.Floor((bounds.Height - 1) / 2), bounds.Width, 1);

        context.FillRectangle(brush, line);
    }
}
