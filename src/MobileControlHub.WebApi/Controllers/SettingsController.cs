using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly IRustDeskService _rustDeskService;
    private readonly ILogService _logService;

    public SettingsController(
        IConfigurationService configService,
        IRustDeskService rustDeskService,
        ILogService logService)
    {
        _configService = configService;
        _rustDeskService = rustDeskService;
        _logService = logService;
    }

    /// <summary>Get current application configuration.</summary>
    [HttpGet]
    public async Task<ActionResult<AppConfiguration>> GetSettings(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        return Ok(config);
    }

    /// <summary>Update application settings.</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResult>> UpdateSettings([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);

        if (request.AdbPath != null) config.AdbPath = request.AdbPath;
        if (request.ScrcpyPath != null) config.ScrcpyPath = request.ScrcpyPath;
        if (request.RustDeskPath != null) config.RustDeskPath = request.RustDeskPath;
        if (request.DevicePollIntervalSeconds.HasValue) config.DevicePollIntervalSeconds = request.DevicePollIntervalSeconds.Value;
        if (request.AdbTimeoutSeconds.HasValue) config.AdbTimeoutSeconds = request.AdbTimeoutSeconds.Value;
        if (request.AutoReconnectDevices.HasValue) config.AutoReconnectDevices = request.AutoReconnectDevices.Value;
        if (request.AutoRestartScrcpy.HasValue) config.AutoRestartScrcpy = request.AutoRestartScrcpy.Value;
        if (request.StartMonitoringOnLaunch.HasValue) config.StartMonitoringOnLaunch = request.StartMonitoringOnLaunch.Value;
        if (request.DefaultScrcpyArgs != null) config.DefaultScrcpyArgs = request.DefaultScrcpyArgs;

        await _configService.SaveAsync(config, ct);
        await _logService.LogInfoAsync("Settings updated via web dashboard", "Settings", ct: ct);
        return Ok(ApiResult.Ok("Settings saved"));
    }

    /// <summary>Get VPS configuration.</summary>
    [HttpGet("vps")]
    public async Task<ActionResult<VpsConfiguration>> GetVpsConfig(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        return Ok(config.VpsConfig);
    }

    /// <summary>Update VPS configuration.</summary>
    [HttpPut("vps")]
    public async Task<ActionResult<ApiResult>> UpdateVpsConfig([FromBody] VpsConfigRequest request, CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);

        config.VpsConfig.Host = request.Host;
        config.VpsConfig.SshPort = request.SshPort;
        config.VpsConfig.RustDeskRelayServer = request.RustDeskRelayServer;
        config.VpsConfig.RustDeskIdServer = request.RustDeskIdServer;
        config.VpsConfig.RustDeskRelayPort = request.RustDeskRelayPort;
        config.VpsConfig.RustDeskIdPort = request.RustDeskIdPort;
        config.VpsConfig.RustDeskApiPort = request.RustDeskApiPort;
        config.VpsConfig.IsConfigured = true;

        await _configService.SaveAsync(config, ct);
        await _logService.LogInfoAsync("VPS configuration updated via web dashboard", "VPS", ct: ct);
        return Ok(ApiResult.Ok("VPS configuration saved"));
    }

    /// <summary>Test VPS connectivity.</summary>
    [HttpPost("vps/test")]
    public async Task<ActionResult<VpsConfiguration>> TestVpsConnectivity(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        var vps = config.VpsConfig;

        if (string.IsNullOrEmpty(vps.Host))
            return BadRequest(ApiResult.Fail("VPS host not configured"));

        vps.IsReachable = await _rustDeskService.TestVpsConnectivityAsync(vps.Host, vps.SshPort, ct);

        var relayHost = string.IsNullOrEmpty(vps.RustDeskRelayServer) ? vps.Host : vps.RustDeskRelayServer;
        vps.IsRelayReachable = await _rustDeskService.TestRelayConnectivityAsync(relayHost, vps.RustDeskRelayPort, ct);

        var idHost = string.IsNullOrEmpty(vps.RustDeskIdServer) ? vps.Host : vps.RustDeskIdServer;
        vps.IsIdServerReachable = await _rustDeskService.TestIdServerConnectivityAsync(idHost, vps.RustDeskIdPort, ct);

        vps.LastTestedAt = DateTime.UtcNow;

        await _configService.SaveAsync(config, ct);
        return Ok(vps);
    }

    /// <summary>Export configuration as JSON.</summary>
    [HttpGet("export")]
    public async Task<ActionResult> ExportConfig(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        return Ok(config);
    }
}
