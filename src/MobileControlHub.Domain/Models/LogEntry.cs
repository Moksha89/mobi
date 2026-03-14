using MobileControlHub.Domain.Enums;

namespace MobileControlHub.Domain.Models;

/// <summary>
/// A structured log entry stored in SQLite.
/// </summary>
public class LogEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public AppLogLevel Level { get; set; } = AppLogLevel.Info;
    public string Category { get; set; } = "General";
    public string Message { get; set; } = string.Empty;
    public string? DeviceSerial { get; set; }
    public string? Details { get; set; }
    public string? Source { get; set; }
}
