using MobileControlHub.Domain.Enums;

namespace MobileControlHub.Domain.Models;

/// <summary>
/// Represents an active scrcpy screen mirroring session for a device.
/// </summary>
public class ScrcpySession
{
    /// <summary>Unique session identifier.</summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Serial number of the target device.</summary>
    public string DeviceSerial { get; set; } = string.Empty;

    /// <summary>Current state of the scrcpy session.</summary>
    public SessionState State { get; set; } = SessionState.Stopped;

    /// <summary>OS process ID of the scrcpy process.</summary>
    public int ProcessId { get; set; }

    /// <summary>When the session was started.</summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the session ended (if applicable).</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Custom scrcpy arguments used for this session.</summary>
    public string CustomArguments { get; set; } = string.Empty;

    /// <summary>Whether auto-restart is enabled for this session.</summary>
    public bool AutoRestart { get; set; }

    /// <summary>Number of times this session has been auto-restarted.</summary>
    public int RestartCount { get; set; }

    /// <summary>Last error message if session is in error state.</summary>
    public string? LastError { get; set; }
}
