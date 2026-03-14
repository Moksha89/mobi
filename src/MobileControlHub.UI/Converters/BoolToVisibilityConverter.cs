using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MobileControlHub.UI.Converters;

/// <summary>
/// Converts bool to Visibility. True = Visible, False = Collapsed.
/// Use ConverterParameter="Inverse" to invert.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        var inverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;

        if (inverse) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Converts bool to a status color brush. True = Green, False = Red.
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isOk = value is bool b && b;
        return isOk ? "#FF4CAF50" : "#FFF44336"; // Green / Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a DeviceConnectionState enum to a display color.
/// </summary>
public class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MobileControlHub.Domain.Enums.DeviceConnectionState state)
        {
            return state switch
            {
                Domain.Enums.DeviceConnectionState.Online => "#FF4CAF50",      // Green
                Domain.Enums.DeviceConnectionState.Offline => "#FFF44336",     // Red
                Domain.Enums.DeviceConnectionState.Unauthorized => "#FFFFC107", // Amber
                Domain.Enums.DeviceConnectionState.Disconnected => "#FF9E9E9E", // Grey
                _ => "#FF9E9E9E"
            };
        }
        return "#FF9E9E9E";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a LogLevel enum to a display color.
/// </summary>
public class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MobileControlHub.Domain.Enums.AppLogLevel level)
        {
            return level switch
            {
                Domain.Enums.AppLogLevel.Debug => "#FF9E9E9E",
                Domain.Enums.AppLogLevel.Info => "#FF2196F3",
                Domain.Enums.AppLogLevel.Warning => "#FFFFC107",
                Domain.Enums.AppLogLevel.Error => "#FFF44336",
                Domain.Enums.AppLogLevel.Critical => "#FFD32F2F",
                _ => "#FFCCCCCC"
            };
        }
        return "#FFCCCCCC";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a battery level integer to a display color.
/// </summary>
public class BatteryLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int level)
        {
            return level switch
            {
                < 0 => "#FF9E9E9E",   // Unknown
                < 20 => "#FFF44336",   // Red
                < 50 => "#FFFFC107",   // Amber
                _ => "#FF4CAF50"       // Green
            };
        }
        return "#FF9E9E9E";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
