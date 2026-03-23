using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Data;
using MobileControlHub.Infrastructure.Services;
using MobileControlHub.UI.ViewModels;

namespace MobileControlHub.UI;

/// <summary>
/// Application entry point. Configures dependency injection and initializes services.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static ServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Initialize database
        var db = _serviceProvider.GetRequiredService<DatabaseManager>();
        await db.InitializeAsync();

        // Log startup
        var logService = _serviceProvider.GetRequiredService<ILogService>();
        await logService.LogInfoAsync("Mobile Control Hub started", category: "App");

        // Create and show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Initialize ViewModels
        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        await mainVm.InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            // Stop monitoring and clean up sessions
            var monitor = _serviceProvider.GetService<IDeviceMonitorService>();
            if (monitor != null)
                await monitor.StopAsync();

            var scrcpy = _serviceProvider.GetService<IScrcpyService>();
            if (scrcpy != null)
                await scrcpy.StopAllSessionsAsync();

            var logService = _serviceProvider.GetService<ILogService>();
            if (logService != null)
                await logService.LogInfoAsync("Mobile Control Hub shutting down", category: "App");

            _serviceProvider.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Database
        services.AddSingleton<DatabaseManager>();

        // Services (register as singletons for shared state)
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IAdbService, AdbService>();
        services.AddSingleton<IScrcpyService, ScrcpyService>();
        services.AddSingleton<IRustDeskService, RustDeskService>();
        services.AddSingleton<IDeviceMonitorService, DeviceMonitorService>();
        services.AddSingleton<StartupService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<SessionsViewModel>();
        services.AddSingleton<VpsSettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }
}
