using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Structured logging service that persists log entries to SQLite.
/// </summary>
public interface ILogService
{
    /// <summary>Write a log entry.</summary>
    Task LogAsync(AppLogLevel level, string message, string? category = null,
        string? deviceSerial = null, string? details = null, string? source = null,
        CancellationToken ct = default);

    /// <summary>Write an info-level log entry.</summary>
    Task LogInfoAsync(string message, string? category = null, string? deviceSerial = null, CancellationToken ct = default);

    /// <summary>Write a warning-level log entry.</summary>
    Task LogWarningAsync(string message, string? category = null, string? deviceSerial = null, CancellationToken ct = default);

    /// <summary>Write an error-level log entry.</summary>
    Task LogErrorAsync(string message, string? details = null, string? deviceSerial = null, string? source = null, CancellationToken ct = default);

    /// <summary>Query log entries with optional filters.</summary>
    Task<List<LogEntry>> GetLogsAsync(AppLogLevel? minLevel = null, string? category = null,
        string? deviceSerial = null, DateTime? from = null, DateTime? to = null,
        int limit = 200, CancellationToken ct = default);

    /// <summary>Get recent log entries.</summary>
    Task<List<LogEntry>> GetRecentLogsAsync(int count = 50, CancellationToken ct = default);

    /// <summary>Clear all logs older than the specified date.</summary>
    Task ClearLogsAsync(DateTime? olderThan = null, CancellationToken ct = default);

    /// <summary>Raised when a new log entry is written (for real-time UI updates).</summary>
    event EventHandler<LogEntry>? LogEntryAdded;
}
