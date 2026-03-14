namespace MobileControlHub.Domain.Models;

/// <summary>
/// Central application configuration persisted in SQLite.
/// </summary>
public class AppConfiguration
{
    // Tool paths
    public string AdbPath { get; set; } = "tools\\adb.exe";
    public string ScrcpyPath { get; set; } = "tools\\scrcpy.exe";
    public string RustDeskPath { get; set; } = "C:\\Program Files\\RustDesk\\rustdesk.exe";

    // Monitoring settings
    public int DevicePollIntervalSeconds { get; set; } = 5;
    public int AdbTimeoutSeconds { get; set; } = 10;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;

    // Auto-reconnect
    public bool AutoReconnectDevices { get; set; } = true;
    public bool AutoRestartScrcpy { get; set; }

    // Startup
    public bool StartOnWindowsBoot { get; set; }
    public bool StartMonitoringOnLaunch { get; set; } = true;
    public bool RestoreSessionsOnLaunch { get; set; }

    // scrcpy defaults
    public string DefaultScrcpyArgs { get; set; } = "--max-size=1024 --max-fps=30";

    // VPS configuration
    public VpsConfiguration VpsConfig { get; set; } = new();

    // Friendly names mapped by serial number
    public Dictionary<string, string> DeviceFriendlyNames { get; set; } = new();
}
