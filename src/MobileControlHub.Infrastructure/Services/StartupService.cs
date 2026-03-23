using MobileControlHub.Domain.Interfaces;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Manages Windows startup registration and session restoration.
/// Uses the Windows Registry Run key for auto-start on login.
/// </summary>
public class StartupService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "MobileControlHub";

    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;

    public StartupService(IConfigurationService configService, ILogService logService)
    {
        _configService = configService;
        _logService = logService;
    }

    /// <summary>
    /// Enable or disable auto-start on Windows boot.
    /// Uses Registry on Windows; no-op on other platforms.
    /// </summary>
    public async Task SetStartOnBootAsync(bool enabled)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                await _logService.LogWarningAsync(
                    "Startup registration is only supported on Windows",
                    category: "Startup");
                return;
            }

            // Use dynamic invocation to avoid compile errors on non-Windows
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                await _logService.LogErrorAsync("Could not determine executable path", source: "StartupService");
                return;
            }

            // Registry operations wrapped for Windows-only compilation
            SetRegistryStartup(enabled, exePath);

            var config = await _configService.LoadAsync();
            config.StartOnWindowsBoot = enabled;
            await _configService.SaveAsync(config);

            await _logService.LogInfoAsync(
                $"Start on boot {(enabled ? "enabled" : "disabled")}",
                category: "Startup");
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync(
                $"Failed to {(enabled ? "enable" : "disable")} startup: {ex.Message}",
                source: "StartupService");
        }
    }

    /// <summary>
    /// Check if the app is registered to start on boot.
    /// </summary>
    public bool IsStartOnBootEnabled()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        return CheckRegistryStartup();
    }

    // Platform-specific registry methods using conditional compilation
    private static void SetRegistryStartup(bool enabled, string exePath)
    {
        #pragma warning disable CA1416 // Platform compatibility
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key == null) return;

            if (enabled)
                key.SetValue(AppName, $"\"{exePath}\" --minimized");
            else
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        #pragma warning restore CA1416
    }

    private static bool CheckRegistryStartup()
    {
        #pragma warning disable CA1416 // Platform compatibility
        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(AppName) != null;
        }
        #pragma warning restore CA1416
        return false;
    }
}
