using MobileControlHub.Domain.Enums;

namespace MobileControlHub.Domain.Models;

/// <summary>
/// Represents a connected Android device discovered via ADB.
/// </summary>
public class AndroidDevice
{
    /// <summary>Unique device serial number from ADB.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Device model name (e.g., "Pixel 7").</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Device manufacturer (e.g., "Google").</summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Android OS version (e.g., "14").</summary>
    public string AndroidVersion { get; set; } = string.Empty;

    /// <summary>Connection type: USB or TCP/IP.</summary>
    public string ConnectionType { get; set; } = "USB";

    /// <summary>ADB authorization state.</summary>
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Unknown;

    /// <summary>Battery level percentage (0-100), or -1 if unknown.</summary>
    public int BatteryLevel { get; set; } = -1;

    /// <summary>Whether the device screen is currently on.</summary>
    public bool? IsScreenOn { get; set; }

    /// <summary>User-assigned friendly name/label for the device.</summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>When the device was first discovered.</summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>When the device info was last refreshed.</summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>Whether a scrcpy session is currently active for this device.</summary>
    public bool HasActiveSession { get; set; }

    /// <summary>Whether this device is selected in the UI for bulk actions.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Display name: friendly name if set, otherwise model or serial.</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(FriendlyName)
        ? FriendlyName
        : !string.IsNullOrWhiteSpace(Model) ? Model : SerialNumber;
}
