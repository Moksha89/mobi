using System.Text.Json;
using Microsoft.Data.Sqlite;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.Infrastructure.Data;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Configuration service that persists app settings in SQLite
/// and supports JSON export/import.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly DatabaseManager _db;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AppConfiguration? _cachedConfig;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigurationService(DatabaseManager db)
    {
        _db = db;
    }

    public async Task<AppConfiguration> LoadAsync(CancellationToken ct = default)
    {
        if (_cachedConfig != null)
            return _cachedConfig;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            var json = await GetSettingInternalAsync("AppConfiguration", ct);
            if (!string.IsNullOrEmpty(json))
            {
                _cachedConfig = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions)
                    ?? new AppConfiguration();
            }
            else
            {
                _cachedConfig = new AppConfiguration();
            }

            // Ensure VPS defaults are populated if host is empty (e.g., from older config)
            if (string.IsNullOrEmpty(_cachedConfig.VpsConfig.Host))
            {
                var defaults = new VpsConfiguration();
                _cachedConfig.VpsConfig.Host = defaults.Host;
                _cachedConfig.VpsConfig.RustDeskRelayServer = defaults.RustDeskRelayServer;
                _cachedConfig.VpsConfig.RustDeskIdServer = defaults.RustDeskIdServer;
                _cachedConfig.VpsConfig.IsConfigured = defaults.IsConfigured;
            }

            return _cachedConfig;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppConfiguration config, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            await SetSettingInternalAsync("AppConfiguration", json, ct);
            _cachedConfig = config;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ExportToJsonAsync(string filePath, CancellationToken ct = default)
    {
        var config = await LoadAsync(ct);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    public async Task<AppConfiguration> ImportFromJsonAsync(string filePath, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(filePath, ct);
        var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration file.");

        await SaveAsync(config, ct);
        return config;
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        return await GetSettingInternalAsync(key, ct);
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        await SetSettingInternalAsync(key, value, ct);
        // Invalidate cache if the main config key is updated
        if (key == "AppConfiguration")
            _cachedConfig = null;
    }

    public async Task<string?> GetDeviceFriendlyNameAsync(string serial, CancellationToken ct = default)
    {
        var config = await LoadAsync(ct);
        return config.DeviceFriendlyNames.GetValueOrDefault(serial);
    }

    public async Task SetDeviceFriendlyNameAsync(string serial, string name, CancellationToken ct = default)
    {
        var config = await LoadAsync(ct);
        config.DeviceFriendlyNames[serial] = name;
        await SaveAsync(config, ct);

        // Also update the DeviceProfiles table
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO DeviceProfiles (SerialNumber, FriendlyName, LastSeen)
            VALUES (@Serial, @Name, datetime('now'))
            ON CONFLICT(SerialNumber) DO UPDATE SET
                FriendlyName = @Name,
                LastSeen = datetime('now');";
        command.Parameters.AddWithValue("@Serial", serial);
        command.Parameters.AddWithValue("@Name", name);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<string?> GetSettingInternalAsync(string key, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = @Key";
        command.Parameters.AddWithValue("@Key", key);

        var result = await command.ExecuteScalarAsync(ct);
        return result?.ToString();
    }

    private async Task SetSettingInternalAsync(string key, string value, CancellationToken ct)
    {
        using var connection = _db.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Settings (Key, Value, UpdatedAt)
            VALUES (@Key, @Value, datetime('now'))
            ON CONFLICT(Key) DO UPDATE SET
                Value = @Value,
                UpdatedAt = datetime('now');";
        command.Parameters.AddWithValue("@Key", key);
        command.Parameters.AddWithValue("@Value", value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
