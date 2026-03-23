using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IAdbService _adbService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IRustDeskService _rustDeskService;
    private readonly IDeviceMonitorService _monitor;
    private readonly IConfigurationService _configService;

    public StatusController(
        IAdbService adbService,
        IScrcpyService scrcpyService,
        IRustDeskService rustDeskService,
        IDeviceMonitorService monitor,
        IConfigurationService configService)
    {
        _adbService = adbService;
        _scrcpyService = scrcpyService;
        _rustDeskService = rustDeskService;
        _monitor = monitor;
        _configService = configService;
    }

    /// <summary>Get overall system health status for the dashboard.</summary>
    [HttpGet]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);

        var adbAvailable = await _adbService.IsAvailableAsync(ct);
        string? adbVersion = null;
        if (adbAvailable)
        {
            try { adbVersion = await _adbService.GetVersionAsync(ct); }
            catch { /* ignore */ }
        }

        var scrcpyAvailable = await _scrcpyService.IsAvailableAsync(ct);
        string? scrcpyVersion = null;
        if (scrcpyAvailable)
        {
            try { scrcpyVersion = await _scrcpyService.GetVersionAsync(ct); }
            catch { /* ignore */ }
        }

        var rustDeskInstalled = await _rustDeskService.IsInstalledAsync(ct);

        return Ok(new SystemStatusResponse
        {
            AdbAvailable = adbAvailable,
            AdbVersion = adbVersion,
            ScrcpyAvailable = scrcpyAvailable,
            ScrcpyVersion = scrcpyVersion,
            RustDeskInstalled = rustDeskInstalled,
            RustDeskRunning = _rustDeskService.IsRunning(),
            VpsConfigured = config.VpsConfig.IsConfigured,
            VpsReachable = config.VpsConfig.IsReachable,
            ConnectedDevices = _monitor.GetCurrentDevices().Count,
            ActiveSessions = _scrcpyService.GetActiveSessions().Count,
            MonitorRunning = _monitor.IsRunning
        });
    }
}
