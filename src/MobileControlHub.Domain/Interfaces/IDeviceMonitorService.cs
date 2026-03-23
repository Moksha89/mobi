using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Background service that monitors device connections, ADB health,
/// and scrcpy session states.
/// </summary>
public interface IDeviceMonitorService
{
    /// <summary>Whether the monitor is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Start the background monitoring loop.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Stop the background monitoring loop.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Force an immediate device refresh.</summary>
    Task RefreshNowAsync(CancellationToken ct = default);

    /// <summary>Get the current list of known devices.</summary>
    IReadOnlyList<AndroidDevice> GetCurrentDevices();

    /// <summary>Raised when the device list changes (connect/disconnect/state change).</summary>
    event EventHandler<List<AndroidDevice>>? DevicesChanged;

    /// <summary>Raised when a device is newly connected.</summary>
    event EventHandler<AndroidDevice>? DeviceConnected;

    /// <summary>Raised when a device is disconnected.</summary>
    event EventHandler<AndroidDevice>? DeviceDisconnected;

    /// <summary>Raised when the ADB server encounters an error.</summary>
    event EventHandler<string>? AdbError;
}
