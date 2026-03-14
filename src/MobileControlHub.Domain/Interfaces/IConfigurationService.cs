using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing application configuration persistence.
/// Stores settings in SQLite and supports JSON export/import.
/// </summary>
public interface IConfigurationService
{
    /// <summary>Load the current application configuration.</summary>
    Task<AppConfiguration> LoadAsync(CancellationToken ct = default);

    /// <summary>Save the application configuration.</summary>
    Task SaveAsync(AppConfiguration config, CancellationToken ct = default);

    /// <summary>Export configuration to a JSON file.</summary>
    Task ExportToJsonAsync(string filePath, CancellationToken ct = default);

    /// <summary>Import configuration from a JSON file.</summary>
    Task<AppConfiguration> ImportFromJsonAsync(string filePath, CancellationToken ct = default);

    /// <summary>Get a specific setting value by key.</summary>
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);

    /// <summary>Set a specific setting value by key.</summary>
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Get the friendly name for a device serial, if set.</summary>
    Task<string?> GetDeviceFriendlyNameAsync(string serial, CancellationToken ct = default);

    /// <summary>Set a friendly name for a device serial.</summary>
    Task SetDeviceFriendlyNameAsync(string serial, string name, CancellationToken ct = default);
}
