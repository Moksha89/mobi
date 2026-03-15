namespace MobileControlHub.WebApi.Models;

/// <summary>API response for system health/dashboard status.</summary>
public class SystemStatusResponse
{
    public bool AdbAvailable { get; set; }
    public string? AdbVersion { get; set; }
    public bool ScrcpyAvailable { get; set; }
    public string? ScrcpyVersion { get; set; }
    public bool RustDeskInstalled { get; set; }
    public bool RustDeskRunning { get; set; }
    public bool VpsConfigured { get; set; }
    public bool VpsReachable { get; set; }
    public int ConnectedDevices { get; set; }
    public int ActiveSessions { get; set; }
    public bool MonitorRunning { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>Request to launch a scrcpy session.</summary>
public class LaunchSessionRequest
{
    public string? CustomArgs { get; set; }
    public bool AutoRestart { get; set; }
}

/// <summary>Request to install an APK.</summary>
public class InstallApkRequest
{
    public string ApkPath { get; set; } = string.Empty;
}

/// <summary>Request to push a file to a device.</summary>
public class PushFileRequest
{
    public string LocalPath { get; set; } = string.Empty;
    public string RemotePath { get; set; } = string.Empty;
}

/// <summary>Request to pull a file from a device.</summary>
public class PullFileRequest
{
    public string RemotePath { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
}

/// <summary>Request to execute a shell command on a device.</summary>
public class ShellCommandRequest
{
    public string Command { get; set; } = string.Empty;
}

/// <summary>Request to set a device friendly name.</summary>
public class SetFriendlyNameRequest
{
    public string FriendlyName { get; set; } = string.Empty;
}

/// <summary>Request to update VPS configuration.</summary>
public class VpsConfigRequest
{
    public string Host { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string RustDeskRelayServer { get; set; } = string.Empty;
    public string RustDeskIdServer { get; set; } = string.Empty;
    public int RustDeskRelayPort { get; set; } = 21117;
    public int RustDeskIdPort { get; set; } = 21116;
    public int RustDeskApiPort { get; set; } = 21118;
}

/// <summary>Request to update app settings.</summary>
public class UpdateSettingsRequest
{
    public string? AdbPath { get; set; }
    public string? ScrcpyPath { get; set; }
    public string? RustDeskPath { get; set; }
    public int? DevicePollIntervalSeconds { get; set; }
    public int? AdbTimeoutSeconds { get; set; }
    public bool? AutoReconnectDevices { get; set; }
    public bool? AutoRestartScrcpy { get; set; }
    public bool? StartMonitoringOnLaunch { get; set; }
    public string? DefaultScrcpyArgs { get; set; }
}

/// <summary>Screen info response.</summary>
public class ScreenInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Tap request (x, y coordinates in device pixels).</summary>
public class TapRequest
{
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Swipe request.</summary>
public class SwipeRequest
{
    public int X1 { get; set; }
    public int Y1 { get; set; }
    public int X2 { get; set; }
    public int Y2 { get; set; }
    public int DurationMs { get; set; } = 300;
}

/// <summary>Key event request (Android keycode).</summary>
public class KeyRequest
{
    public int KeyCode { get; set; }
}

/// <summary>Text input request.</summary>
public class TextRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Log query filter parameters.</summary>
public class LogQueryParams
{
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string? DeviceSerial { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Limit { get; set; } = 200;
}

/// <summary>Generic API result wrapper.</summary>
public class ApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ApiResult Ok(string message = "OK") => new() { Success = true, Message = message };
    public static ApiResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>Generic API result wrapper with data.</summary>
public class ApiResult<T> : ApiResult
{
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T data, string message = "OK") => new() { Success = true, Message = message, Data = data };
    public new static ApiResult<T> Fail(string message) => new() { Success = false, Message = message };
}
