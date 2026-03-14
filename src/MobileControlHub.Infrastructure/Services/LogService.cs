using Microsoft.Data.Sqlite;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.Infrastructure.Data;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Structured logging service that persists entries to SQLite.
/// Thread-safe with a write queue for non-blocking UI updates.
/// </summary>
public class LogService : ILogService
{
    private readonly DatabaseManager _db;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event EventHandler<LogEntry>? LogEntryAdded;

    public LogService(DatabaseManager db)
    {
        _db = db;
    }

    public async Task LogAsync(AppLogLevel level, string message, string? category = null,
        string? deviceSerial = null, string? details = null, string? source = null,
        CancellationToken ct = default)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category ?? "General",
            Message = message,
            DeviceSerial = deviceSerial,
            Details = details,
            Source = source
        };

        await _writeLock.WaitAsync(ct);
        try
        {
            using var connection = _db.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO LogEntries (Timestamp, Level, Category, Message, DeviceSerial, Details, Source)
                VALUES (@Timestamp, @Level, @Category, @Message, @DeviceSerial, @Details, @Source);
                SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@Timestamp", entry.Timestamp.ToString("o"));
            command.Parameters.AddWithValue("@Level", entry.Level.ToString());
            command.Parameters.AddWithValue("@Category", entry.Category);
            command.Parameters.AddWithValue("@Message", entry.Message);
            command.Parameters.AddWithValue("@DeviceSerial", (object?)entry.DeviceSerial ?? DBNull.Value);
            command.Parameters.AddWithValue("@Details", (object?)entry.Details ?? DBNull.Value);
            command.Parameters.AddWithValue("@Source", (object?)entry.Source ?? DBNull.Value);

            var id = await command.ExecuteScalarAsync(ct);
            entry.Id = Convert.ToInt64(id);
        }
        finally
        {
            _writeLock.Release();
        }

        // Raise event for real-time UI updates
        LogEntryAdded?.Invoke(this, entry);
    }

    public Task LogInfoAsync(string message, string? category = null, string? deviceSerial = null, CancellationToken ct = default)
        => LogAsync(AppLogLevel.Info, message, category, deviceSerial, ct: ct);

    public Task LogWarningAsync(string message, string? category = null, string? deviceSerial = null, CancellationToken ct = default)
        => LogAsync(AppLogLevel.Warning, message, category, deviceSerial, ct: ct);

    public Task LogErrorAsync(string message, string? details = null, string? deviceSerial = null, string? source = null, CancellationToken ct = default)
        => LogAsync(AppLogLevel.Error, message, details: details, deviceSerial: deviceSerial, source: source, ct: ct);

    public async Task<List<LogEntry>> GetLogsAsync(AppLogLevel? minLevel = null, string? category = null,
        string? deviceSerial = null, DateTime? from = null, DateTime? to = null,
        int limit = 200, CancellationToken ct = default)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (minLevel.HasValue)
        {
            // Map to list of valid levels
            var validLevels = Enum.GetValues<AppLogLevel>()
                .Where(l => l >= minLevel.Value)
                .Select(l => $"'{l}'");
            conditions.Add($"Level IN ({string.Join(",", validLevels)})");
        }
        if (!string.IsNullOrEmpty(category))
        {
            conditions.Add("Category = @Category");
            command.Parameters.AddWithValue("@Category", category);
        }
        if (!string.IsNullOrEmpty(deviceSerial))
        {
            conditions.Add("DeviceSerial = @DeviceSerial");
            command.Parameters.AddWithValue("@DeviceSerial", deviceSerial);
        }
        if (from.HasValue)
        {
            conditions.Add("Timestamp >= @From");
            command.Parameters.AddWithValue("@From", from.Value.ToString("o"));
        }
        if (to.HasValue)
        {
            conditions.Add("Timestamp <= @To");
            command.Parameters.AddWithValue("@To", to.Value.ToString("o"));
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        command.CommandText = $"SELECT * FROM LogEntries {whereClause} ORDER BY Timestamp DESC LIMIT @Limit";
        command.Parameters.AddWithValue("@Limit", limit);

        var entries = new List<LogEntry>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            entries.Add(ReadLogEntry(reader));
        }

        return entries;
    }

    public async Task<List<LogEntry>> GetRecentLogsAsync(int count = 50, CancellationToken ct = default)
    {
        return await GetLogsAsync(limit: count, ct: ct);
    }

    public async Task ClearLogsAsync(DateTime? olderThan = null, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            using var connection = _db.GetConnection();
            using var command = connection.CreateCommand();

            if (olderThan.HasValue)
            {
                command.CommandText = "DELETE FROM LogEntries WHERE Timestamp < @OlderThan";
                command.Parameters.AddWithValue("@OlderThan", olderThan.Value.ToString("o"));
            }
            else
            {
                command.CommandText = "DELETE FROM LogEntries";
            }

            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static LogEntry ReadLogEntry(SqliteDataReader reader)
    {
        return new LogEntry
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
            Level = Enum.TryParse<AppLogLevel>(reader.GetString(reader.GetOrdinal("Level")), out var level)
                ? level : AppLogLevel.Info,
            Category = reader.GetString(reader.GetOrdinal("Category")),
            Message = reader.GetString(reader.GetOrdinal("Message")),
            DeviceSerial = reader.IsDBNull(reader.GetOrdinal("DeviceSerial"))
                ? null : reader.GetString(reader.GetOrdinal("DeviceSerial")),
            Details = reader.IsDBNull(reader.GetOrdinal("Details"))
                ? null : reader.GetString(reader.GetOrdinal("Details")),
            Source = reader.IsDBNull(reader.GetOrdinal("Source"))
                ? null : reader.GetString(reader.GetOrdinal("Source"))
        };
    }
}
