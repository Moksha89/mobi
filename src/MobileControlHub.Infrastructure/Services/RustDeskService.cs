using System.Diagnostics;
using System.Net.Sockets;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Helpers;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Implementation of IRustDeskService.
/// Wraps the installed RustDesk client -- does NOT rewrite RustDesk functionality.
/// Provides helpers to launch, check status, and test connectivity.
/// </summary>
public class RustDeskService : IRustDeskService
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;

    public RustDeskService(IConfigurationService configService, ILogService logService)
    {
        _configService = configService;
        _logService = logService;
    }

    public async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        var config = await _configService.LoadAsync(ct);
        return ProcessRunner.ExecutableExists(config.RustDeskPath);
    }

    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        var config = await _configService.LoadAsync(ct);
        if (!ProcessRunner.ExecutableExists(config.RustDeskPath))
            return null;

        var result = await ProcessRunner.RunAsync(config.RustDeskPath, "--version", timeoutMs: 5000, ct: ct);
        return result.Success ? result.StandardOutput.Trim() : null;
    }

    public bool IsRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("rustdesk");
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LaunchAsync(CancellationToken ct = default)
    {
        var config = await _configService.LoadAsync(ct);

        if (!ProcessRunner.ExecutableExists(config.RustDeskPath))
        {
            await _logService.LogErrorAsync("RustDesk executable not found", source: "RustDeskService");
            return false;
        }

        if (IsRunning())
        {
            await _logService.LogInfoAsync("RustDesk is already running", category: "RustDesk");
            return true;
        }

        var process = ProcessRunner.StartDetached(config.RustDeskPath, "");
        if (process != null)
        {
            await _logService.LogInfoAsync("RustDesk client launched", category: "RustDesk");
            return true;
        }

        await _logService.LogErrorAsync("Failed to launch RustDesk client", source: "RustDeskService");
        return false;
    }

    public async Task<bool> TestRelayConnectivityAsync(string host, int port, CancellationToken ct = default)
    {
        return await TestTcpConnectivityAsync(host, port, ct);
    }

    public async Task<bool> TestIdServerConnectivityAsync(string host, int port, CancellationToken ct = default)
    {
        return await TestTcpConnectivityAsync(host, port, ct);
    }

    public async Task<bool> TestVpsConnectivityAsync(string host, int port, CancellationToken ct = default)
    {
        return await TestTcpConnectivityAsync(host, port, ct);
    }

    public string GetStatusDescription()
    {
        if (IsRunning())
            return "Running";
        return "Not Running";
    }

    /// <summary>
    /// Test TCP connectivity to a host:port with a timeout.
    /// </summary>
    private static async Task<bool> TestTcpConnectivityAsync(string host, int port, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(5000);

            await client.ConnectAsync(host, port, timeoutCts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
