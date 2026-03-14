namespace MobileControlHub.Domain.Enums;

/// <summary>
/// Represents the connection/authorization state of an Android device.
/// </summary>
public enum DeviceConnectionState
{
    Unknown,
    Online,
    Offline,
    Unauthorized,
    Disconnected,
    Recovery,
    Sideload,
    NoPermissions
}
