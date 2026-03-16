using Microsoft.Data.Sqlite;

namespace MobileControlHub.Infrastructure.Data;

/// <summary>
/// Manages SQLite database initialization, migrations, and connection pooling.
/// </summary>
public class DatabaseManager : IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public DatabaseManager(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MobileControlHub", "mch.db");

        // Ensure directory exists
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={_dbPath}";
    }

    /// <summary>
    /// Get a new database connection.
    /// </summary>
    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Initialize the database schema. Call once at application startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using var connection = GetConnection();

        // Enable WAL mode for better concurrent access
        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", ct);

        // Create settings table
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY NOT NULL,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );", ct);

        // Create log entries table
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS LogEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL DEFAULT (datetime('now')),
                Level TEXT NOT NULL DEFAULT 'Info',
                Category TEXT NOT NULL DEFAULT 'General',
                Message TEXT NOT NULL,
                DeviceSerial TEXT,
                Details TEXT,
                Source TEXT
            );", ct);

        // Create device profiles table (friendly names and metadata)
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS DeviceProfiles (
                SerialNumber TEXT PRIMARY KEY NOT NULL,
                FriendlyName TEXT,
                FirstSeen TEXT NOT NULL DEFAULT (datetime('now')),
                LastSeen TEXT NOT NULL DEFAULT (datetime('now')),
                Notes TEXT
            );", ct);

        // Create Twilio phone number assignments table
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS TwilioNumbers (
                PhoneNumber TEXT PRIMARY KEY NOT NULL,
                ContainerName TEXT NOT NULL UNIQUE,
                FriendlyName TEXT NOT NULL,
                TwilioSid TEXT NOT NULL,
                AssignedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );", ct);

        // Create SMS messages table
        await ExecuteNonQueryAsync(connection, @"
            CREATE TABLE IF NOT EXISTS SmsMessages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PhoneNumber TEXT NOT NULL,
                FromNumber TEXT NOT NULL,
                ToNumber TEXT NOT NULL,
                Body TEXT NOT NULL DEFAULT '',
                Direction TEXT NOT NULL DEFAULT 'inbound',
                ReceivedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );", ct);

        // Create indexes for common queries
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_Timestamp ON LogEntries(Timestamp DESC);", ct);
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_Level ON LogEntries(Level);", ct);
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_LogEntries_DeviceSerial ON LogEntries(DeviceSerial);", ct);
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_SmsMessages_PhoneNumber ON SmsMessages(PhoneNumber);", ct);
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_SmsMessages_ReceivedAt ON SmsMessages(ReceivedAt DESC);", ct);
        await ExecuteNonQueryAsync(connection,
            "CREATE INDEX IF NOT EXISTS IX_TwilioNumbers_Container ON TwilioNumbers(ContainerName);", ct);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    public void Dispose()
    {
        // SQLite connections are pooled and managed per-use
        GC.SuppressFinalize(this);
    }
}
