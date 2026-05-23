using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Converters;

public static class ControlConverters
{
    public static readonly LatheModeToStringConverter LatheModeToString = new();
    public static readonly LogicalNotConverter LogicalNot = new();
    public static readonly BoolToVisibleConverter BoolToVisible = new();
    public static readonly GrblStateToColorConverter GrblStateToColor = new();
    public static readonly GrblStateToStringConverter GrblStateToString = new();
    public static readonly HomedStateToColorConverter HomedStateToColor = new();
    public static readonly IsHomingEnabledConverter IsHomingEnabled = new();
    public static readonly LogicalOrConverter LogicalOr = new();
    public static readonly IsAxisVisibleConverter IsAxisVisible = new();
    public static readonly IsSignalVisibleConverter IsSignalVisible = new();
    public static readonly EnumFlagsHasFlagConverter EnumFlagsHasFlag = new();
    public static readonly EnumValueToBooleanConverter EnumToChecked = new();
    public static readonly EncoderModeToColorConverter EncoderModeToColor = new();
    public static readonly BoolToBrushConverter BoolToBrush = new();
    public static readonly GrblStateToIsJoggingConverter GrblStateToIsJogging = new();
    public static readonly AxisLetterToJogPlusConverter AxisLetterToJogPlus = new();
    public static readonly AxisLetterToJogMinusConverter AxisLetterToJogMinus = new();
    public static readonly HomedStateToBooleanConverter HomedStateToBoolean = new();
    public static readonly ActualRpmDisplayConverter ActualRpmDisplay = new();
    public static readonly SpindleStateIsActiveConverter SpindleStateIsActive = new();

    internal static IBrush ToBrush(UiColor c) => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

    internal static IBrush ThemeBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true
            && value is IBrush brush)
            return brush;
        return fallback;
    }
}

public class HomedStateToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is HomedState homed && homed == HomedState.Homed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LatheModeToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LatheMode mode && mode != LatheMode.Disabled)
            return mode == LatheMode.Radius ? "Radius" : "Diameter";
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AxisLetterToJogPlusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? s + "+" : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class AxisLetterToJogMinusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? s + "-" : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GrblStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is GrblState state
            ? ControlConverters.ToBrush(state.UiColor)
            : ControlConverters.ThemeBrush("ThemeControlMidBrush", Brushes.Transparent);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class IsHomingEnabledConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var state = values.Count > 0 && values[0] is GrblState gs ? gs.State : GrblStates.Unknown;
        var result = state == GrblStates.Alarm && values[0] is GrblState alarm && alarm.Substate == 11;

        if (!result && GrblInfo.HomingEnabled && values.Count > 2 &&
            values[1] is bool jobRunning && !jobRunning &&
            values[2] is bool sleep && !sleep &&
            values[0] is GrblState grbl)
        {
            result = state != GrblStates.Unknown && !grbl.MPG &&
                     (state == GrblStates.Idle || state == GrblStates.Alarm || !GrblInfo.IsGrblHAL);
        }

        return result;
    }
}

public class HomedStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HomedState homed)
        {
            return homed switch
            {
                HomedState.NotHomed => ControlConverters.ThemeBrush("IoSenderNotHomedBrush", Brushes.Transparent),
                HomedState.Homed => ControlConverters.ThemeBrush("IoSenderHomedBrush", Brushes.Transparent),
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EncoderModeToColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is GrblEncoderMode mode0 && values[1] is GrblEncoderMode mode1 &&
            mode0 != GrblEncoderMode.Unknown && mode0.Equals(mode1))
            return Brushes.Salmon;
        return ControlConverters.ThemeBrush("IoSenderReadOnlyBrush",
            ControlConverters.ThemeBrush("ThemeControlMidBrush", Brushes.Transparent));
    }
}

public class GrblStateToStringConverter : IValueConverter
{
    static readonly Dictionary<GrblStates, string> StateNames = new()
    {
        { GrblStates.Unknown, "Unknown" },
        { GrblStates.Idle, "Idle" },
        { GrblStates.Run, "Run" },
        { GrblStates.Tool, "Tool" },
        { GrblStates.Hold, "Hold" },
        { GrblStates.Home, "Home" },
        { GrblStates.Check, "Check" },
        { GrblStates.Jog, "Jog" },
        { GrblStates.Alarm, "Alarm" },
        { GrblStates.Door, "Door" },
        { GrblStates.Sleep, "Sleep" }
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not GrblState state)
            return string.Empty;

        StateNames.TryGetValue(state.State, out var result);
        result ??= state.State.ToString().ToUpperInvariant();
        var substate = state.State == GrblStates.Alarm && state.LastAlarm > 0 ? state.LastAlarm : state.Substate;
        return result + (substate == -1 ? "" : ":" + substate);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GrblStateToIsJoggingConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is GrblState state && state.State == GrblStates.Jog;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class LogicalNotConverter : IValueConverter
{
    public IValueConverter? FinalConverter { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value switch
        {
            bool b => !b,
            null => true,
            int i => i == 0,
            _ => false,
        };
        return FinalConverter == null ? result : FinalConverter.Convert(result, targetType, parameter, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class LogicalOrConverter : IMultiValueConverter
{
    public IValueConverter? FinalConverter { get; set; }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = values.Any(v => v is bool b && b);
        return FinalConverter == null ? result : FinalConverter.Convert(result, targetType, parameter, culture);
    }
}

public class BoolToVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}

public class BoolToBrushConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count != 3)
            return Brushes.Black;

        var pick = values[0] is bool on && on ? values[1] : values[2];
        return pick switch
        {
            IBrush brush => brush,
            string name => BrushFromName(name),
            _ => Brushes.Black
        };
    }

    static IBrush BrushFromName(string name) => name switch
    {
        "Red" => Brushes.Red,
        "Black" => Brushes.Black,
        "White" => Brushes.White,
        _ => new SolidColorBrush(Color.Parse(name))
    };
}

/// <summary>
/// Multi-binding: EnumFlags holder + flag value → bool (replaces WPF-style indexer bindings).
/// </summary>
public class EnumFlagsHasFlagConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] == null || values[1] == null)
            return false;

        if (values[0] is EnumFlags<AxisFlags> axisFlags && values[1] is AxisFlags axis)
            return axisFlags.Value.HasFlag(axis);

        if (values[0] is EnumFlags<Signals> signals && values[1] is Signals signal)
            return signals.Value.HasFlag(signal);

        if (values[0] is EnumFlags<THCSignals> thc && values[1] is THCSignals thcSignal)
            return thc.Value.HasFlag(thcSignal);

        if (values[0] is EnumFlags<GCode.SpindleState> spindle && values[1] is GCode.SpindleState spindleFlag)
            return spindle.Value.HasFlag(spindleFlag);

        return false;
    }
}

public class IsAxisVisibleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var enabled = false;
        if (values.Count == 2 && values[0] is int flags && values[1] is int axis)
            enabled = flags >= axis && (flags & axis) != 0;
        if (values.Count == 2 && values[0] is AxisFlags af && values[1] is AxisFlags ax)
            enabled = af.HasFlag(ax);
        return enabled;
    }
}

public class IsSignalVisibleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var enabled = false;
        if (values.Count == 2 && values[0] is int flags && values[1] is int signal)
            enabled = flags >= signal && (flags & signal) != 0;
        if (values.Count == 2 && values[0] is EnumFlags<Signals> optional && values[1] is Signals sig)
            enabled = optional.Value.HasFlag(sig);
        return enabled;
    }
}

/// <summary>CW/CCW/Off are mutually exclusive for UI highlight (Off is not combined with direction flags).</summary>
public class SpindleStateIsActiveConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not EnumFlags<GCode.SpindleState> holder || values[1] is not GCode.SpindleState flag)
            return false;

        var state = holder.Value;
        return flag switch
        {
            GCode.SpindleState.CW => state.HasFlag(GCode.SpindleState.CW),
            GCode.SpindleState.CCW => state.HasFlag(GCode.SpindleState.CCW),
            GCode.SpindleState.Off => !state.HasFlag(GCode.SpindleState.CW) && !state.HasFlag(GCode.SpindleState.CCW),
            _ => false
        };
    }
}

public class ActualRpmDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double rpm || double.IsNaN(rpm))
            return "RPM:NaN";
        return $"RPM:{rpm:0}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class EnumValueToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        return value.ToString()?.Equals(parameter.ToString(), StringComparison.InvariantCultureIgnoreCase) == true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool use || !use || parameter == null)
            return BindingOperations.DoNothing;
        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
