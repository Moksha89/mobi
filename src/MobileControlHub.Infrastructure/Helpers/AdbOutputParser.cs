using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Models;

namespace MobileControlHub.Infrastructure.Helpers;

/// <summary>
/// Parses raw ADB command output into domain models.
/// </summary>
public static class AdbOutputParser
{
    /// <summary>
    /// Parse the output of 'adb devices -l' into a list of AndroidDevice objects.
    /// Example line: "SERIAL123  device usb:1-1 product:raven model:Pixel_6_Pro device:raven transport_id:1"
    /// </summary>
    public static List<AndroidDevice> ParseDeviceList(string output)
    {
        var devices = new List<AndroidDevice>();
        if (string.IsNullOrWhiteSpace(output))
            return devices;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip the header line and empty lines
            if (trimmed.StartsWith("List of devices") || string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Skip lines that don't look like device entries
            if (!trimmed.Contains('\t') && !trimmed.Contains("device") &&
                !trimmed.Contains("unauthorized") && !trimmed.Contains("offline"))
                continue;

            var device = ParseDeviceLine(trimmed);
            if (device != null)
                devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// Parse a single device line from 'adb devices -l'.
    /// </summary>
    private static AndroidDevice? ParseDeviceLine(string line)
    {
        // Split by whitespace: "SERIAL  state  key:value key:value ..."
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var serial = parts[0];
        var stateStr = parts[1];

        var device = new AndroidDevice
        {
            SerialNumber = serial,
            ConnectionState = ParseConnectionState(stateStr),
            ConnectionType = serial.Contains(':') ? "TCP" : "USB"
        };

        // Parse key:value pairs from the extended output
        for (int i = 2; i < parts.Length; i++)
        {
            var kv = parts[i].Split(':', 2);
            if (kv.Length != 2) continue;

            switch (kv[0].ToLowerInvariant())
            {
                case "model":
                    device.Model = kv[1].Replace('_', ' ');
                    break;
                case "product":
                    // Additional product info
                    break;
                case "device":
                    // Code name
                    break;
            }
        }

        return device;
    }

    /// <summary>
    /// Parse the ADB device state string into our enum.
    /// </summary>
    public static DeviceConnectionState ParseConnectionState(string state)
    {
        return state.Trim().ToLowerInvariant() switch
        {
            "device" => DeviceConnectionState.Online,
            "offline" => DeviceConnectionState.Offline,
            "unauthorized" => DeviceConnectionState.Unauthorized,
            "recovery" => DeviceConnectionState.Recovery,
            "sideload" => DeviceConnectionState.Sideload,
            "no permissions" or "nopermissions" => DeviceConnectionState.NoPermissions,
            _ => DeviceConnectionState.Unknown
        };
    }

    /// <summary>
    /// Parse battery level from 'adb shell dumpsys battery' output.
    /// </summary>
    public static int ParseBatteryLevel(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return -1;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("level:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["level:".Length..].Trim();
                if (int.TryParse(value, out var level))
                    return level;
            }
        }

        return -1;
    }

    /// <summary>
    /// Parse screen state from 'adb shell dumpsys display' or 'dumpsys power' output.
    /// </summary>
    public static bool ParseScreenState(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return false;

        // Check for "mScreenOn=true" or "Display Power: state=ON"
        return output.Contains("mScreenOn=true", StringComparison.OrdinalIgnoreCase)
            || output.Contains("state=ON", StringComparison.OrdinalIgnoreCase)
            || output.Contains("mWakefulness=Awake", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parse a property value from 'adb shell getprop' output.
    /// </summary>
    public static string ParseProperty(string output, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith($"[{propertyName}]"))
            {
                var start = trimmed.IndexOf('[', propertyName.Length + 2);
                var end = trimmed.LastIndexOf(']');
                if (start >= 0 && end > start)
                    return trimmed[(start + 1)..end];
            }
        }

        // If single-property query (no brackets), return the whole trimmed output
        return output.Trim().TrimStart('[').TrimEnd(']');
    }
}
