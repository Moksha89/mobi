using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing scrcpy screen mirroring sessions.
/// Wraps scrcpy.exe for launching, tracking, and controlling mirroring sessions.
/// </summary>
public interface IScrcpyService
{
    /// <summary>Check if scrcpy executable is available at the configured path.</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>Get the scrcpy version string.</summary>
    Task<string> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Launch a scrcpy session for a specific device.</summary>
    Task<ScrcpySession> LaunchSessionAsync(string deviceSerial, string? customArgs = null, bool autoRestart = false, CancellationToken ct = default);

    /// <summary>Stop a running scrcpy session.</summary>
    Task<bool> StopSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Stop all running scrcpy sessions.</summary>
    Task StopAllSessionsAsync(CancellationToken ct = default);

    /// <summary>Get all active sessions.</summary>
    IReadOnlyList<ScrcpySession> GetActiveSessions();

    /// <summary>Get the session for a specific device, if any.</summary>
    ScrcpySession? GetSessionForDevice(string deviceSerial);

    /// <summary>Check if a session process is still running.</summary>
    bool IsSessionAlive(string sessionId);

    /// <summary>Restart a session that has stopped or errored.</summary>
    Task<ScrcpySession?> RestartSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Event raised when a session state changes.</summary>
    event EventHandler<ScrcpySession>? SessionStateChanged;
}
