using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CNC.Core;

public class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Dictionary<string, ICollection<string>> _validationErrors = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ClearErrors()
    {
        List<string> properties = new();

        foreach (var error in _validationErrors)
            if (!properties.Contains(error.Key))
                properties.Add(error.Key);

        _validationErrors.Clear();

        foreach (var property in properties)
            if (!string.IsNullOrEmpty(property))
                RaiseErrorsChanged(property);
    }

    public void SetError(string message)
    {
        _validationErrors.Add(string.Empty, new List<string> { message });
    }

    public void SetError(string property, string message)
    {
        if (_validationErrors.TryGetValue(property, out var value))
            value.Add(message);
        else
            _validationErrors.Add(property, new List<string> { message });

        RaiseErrorsChanged(property);
    }

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    private void RaiseErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || !_validationErrors.ContainsKey(propertyName))
            return Array.Empty<string>();

        return _validationErrors[propertyName];
    }

    public bool HasErrors => _validationErrors.Count > 0;
}

public static class dbl
{
    public static string ToInvariantString(this double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string ToInvariantString(this double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);

    public static bool Assign(double value, ref double holder)
    {
        bool changed;

        if ((changed = double.IsNaN(value) ? !double.IsNaN(holder) : holder != value))
            holder = value;

        return changed;
    }

    public static double[] ParseList(string s)
    {
        string[] v = s.Split(',');
        double[] values = new double[v.Length];

        for (int i = 0; i < v.Length; i++)
        {
            if (!double.TryParse(v[i], NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[i]))
                values[i] = 0.0d;
        }

        return values;
    }

    public static double Parse(string? value)
    {
        double result = double.NaN;

        if (value != null)
        {
            value = value.Trim();

            if (value.Length == 0 || !double.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
                result = double.NaN;
        }

        return result;
    }
}

public class EnumFlags<T> : ViewModelBase where T : struct, Enum
{
    private T value;

    public EnumFlags(T t)
    {
        if (!typeof(T).IsEnum) throw new ArgumentException($"{nameof(T)} must be an enum type");
        value = t;
    }

    public T Value
    {
        get { return value; }
        set
        {
            if (!EqualityComparer<T>.Default.Equals(this.value, value))
            {
                this.value = value;
                OnPropertyChanged("Item[]");
            }
        }
    }

    public bool this[T key]
    {
        get => (((int)(object)value & (int)(object)key) == (int)(object)key);
        set
        {
            if ((((int)(object)this.value & (int)(object)key) == (int)(object)key) == value) return;

            this.value = (T)(object)((int)(object)this.value ^ (int)(object)key);
            OnPropertyChanged("Item[]");
        }
    }
}

public static class FileUtils
{
    public static bool IsAllowedFile(string filename, string extensions)
    {
        int pos = filename.LastIndexOf('.');

        return pos > 0 && ("," + extensions + ",").Contains("," + filename.Substring(pos + 1).ToLower() + ",");
    }

    public static string ExtensionsToFilter(string extensions)
    {
        string[] filetypes = extensions.Split(',');

        for (int i = 0; i < filetypes.Length; i++)
            filetypes[i] = "*." + filetypes[i];

        return string.Join(";", filetypes);
    }

    public static StreamReader? OpenFile(string filename)
    {
        try
        {
            return new StreamReader(filename);
        }
        catch
        {
            return null;
        }
    }
}

public static class StringEnumConversion
{
    public static int ConvertToEnum<T>(object value) where T : struct, Enum
    {
        Debug.Assert(typeof(T).IsEnum);
        Debug.Assert(value != null);
        return (int)Enum.Parse(typeof(T), value.ToString()!);
    }
}

public class Copy
{
    public static void Properties<T>(T source, T target)
    {
        var type = typeof(T);
        foreach (var sourceProperty in type.GetProperties())
        {
            if (sourceProperty.CanRead)
            {
                var targetProperty = type.GetProperty(sourceProperty.Name);
                if (targetProperty?.CanWrite == true)
                    targetProperty.SetValue(target, sourceProperty.GetValue(source, null), null);
            }
        }
    }
}

public static class WaitFor
{
    public static bool SingleEvent<TEvent>(this CancellationToken token, System.Action<TEvent>? handler, System.Action<System.Action<TEvent>> subscribe, System.Action<System.Action<TEvent>> unsubscribe, int msTimeout, System.Action? initializer = null)
    {
        var q = new BlockingCollection<TEvent>();
        Action<TEvent> add = item => q.TryAdd(item);
        subscribe(add);
        try
        {
            initializer?.Invoke();
            if (q.TryTake(out var eventResult, msTimeout, token))
            {
                handler?.Invoke(eventResult);
                return true;
            }
            return false;
        }
        finally
        {
            unsubscribe(add);
            q.Dispose();
        }
    }

    public static bool AckResponse<TEvent>(this CancellationToken token, System.Action<TEvent>? handler, System.Action<System.Action<TEvent>> subscribe, System.Action<System.Action<TEvent>> unsubscribe, int msTimeout, System.Action? initializer = null)
    {
        var q = new BlockingCollection<TEvent>();
        Action<TEvent> add = item => q.TryAdd(item);
        subscribe(add);
        try
        {
            initializer?.Invoke();
            while (q.TryTake(out var eventResult, msTimeout, token))
            {
                handler?.Invoke(eventResult);
                if ((string)(object)eventResult! == "ok")
                    return true;
            }
            return false;
        }
        finally
        {
            unsubscribe(add);
            q.Dispose();
        }
    }

    public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<object?>();
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tcs.TrySetResult(null);
        if (cancellationToken != default)
            cancellationToken.Register(() => tcs.TrySetCanceled());

        return tcs.Task;
    }
}
