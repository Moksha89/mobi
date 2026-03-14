namespace MobileControlHub.Domain.Enums;

/// <summary>
/// Represents the state of a scrcpy mirroring session.
/// </summary>
public enum SessionState
{
    Stopped,
    Starting,
    Running,
    Error,
    Reconnecting
}
