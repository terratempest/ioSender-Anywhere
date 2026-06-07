using System.ComponentModel;

namespace CNC.Controls.Avalonia.Utilities;

internal static class PropertyChangedSubscription
{
    public static void Swap<T>(
        ref T? current,
        T? next,
        PropertyChangedEventHandler handler)
        where T : class, INotifyPropertyChanged
    {
        if (ReferenceEquals(current, next))
            return;

        if (current is not null)
            current.PropertyChanged -= handler;

        current = next;

        if (current is not null)
            current.PropertyChanged += handler;
    }
}
