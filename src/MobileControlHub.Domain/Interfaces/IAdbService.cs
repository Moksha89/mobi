using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for interacting with Android Debug Bridge (ADB).
/// Wraps adb.exe commands for device discovery, info retrieval, and device actions.
/// </summary>
public interface IAdbService
{
    /// <summary>Check if ADB executable is available at the configured path.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Get the ADB version string.</summary>
    Task<string> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Discover all connected devices via 'adb devices -l'.</summary>
    Task<List<AndroidDevice>> GetConnectedDevicesAsync(CancellationToken ct = default);

    /// <summary>Get detailed properties for a specific device.</summary>
    Task<AndroidDevice> GetDeviceDetailsAsync(string serial, CancellationToken ct = default);

    /// <summary>Get battery level for a device.</summary>
    Task<int> GetBatteryLevelAsync(string serial, CancellationToken ct = default);

    /// <summary>Check if the device screen is on.</summary>
    Task<bool> IsScreenOnAsync(string serial, CancellationToken ct = default);

    /// <summary>Execute a shell command on the device.</summary>
    Task<CommandResult> ExecuteShellCommandAsync(string serial, string command, CancellationToken ct = default);

    /// <summary>Install an APK on the device.</summary>
    Task<CommandResult> InstallApkAsync(string serial, string apkPath, CancellationToken ct = default);

    /// <summary>Push a file to the device.</summary>
    Task<CommandResult> PushFileAsync(string serial, string localPath, string remotePath, CancellationToken ct = default);

    /// <summary>Pull a file from the device.</summary>
    Task<CommandResult> PullFileAsync(string serial, string remotePath, string localPath, CancellationToken ct = default);

    /// <summary>Capture a screenshot and save it locally.</summary>
    Task<CommandResult> CaptureScreenshotAsync(string serial, string savePath, CancellationToken ct = default);

    /// <summary>Reboot the device.</summary>
    Task<CommandResult> RebootDeviceAsync(string serial, CancellationToken ct = default);

    /// <summary>Reconnect a device via ADB.</summary>
    Task<CommandResult> ReconnectDeviceAsync(string serial, CancellationToken ct = default);

    /// <summary>Start the ADB server.</summary>
    Task<CommandResult> StartServerAsync(CancellationToken ct = default);

    /// <summary>Kill the ADB server.</summary>
    Task<CommandResult> KillServerAsync(CancellationToken ct = default);

    /// <summary>Connect to a remote device via ADB TCP/IP (adb connect host:port).</summary>
    Task<CommandResult> ConnectDeviceAsync(string host, int port, CancellationToken ct = default);

    /// <summary>Disconnect a remote device via ADB (adb disconnect host:port).</summary>
    Task<CommandResult> DisconnectDeviceAsync(string host, int port, CancellationToken ct = default);
}
