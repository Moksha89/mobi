using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.WebApi.Hubs;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IAdbService _adbService;
    private readonly IDeviceMonitorService _monitor;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly IHubContext<DeviceHub> _hubContext;

    public DevicesController(
        IAdbService adbService,
        IDeviceMonitorService monitor,
        IConfigurationService configService,
        ILogService logService,
        IHubContext<DeviceHub> hubContext)
    {
        _adbService = adbService;
        _monitor = monitor;
        _configService = configService;
        _logService = logService;
        _hubContext = hubContext;
    }

    /// <summary>Get all connected devices.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<AndroidDevice>> GetDevices()
    {
        return Ok(_monitor.GetCurrentDevices());
    }

    /// <summary>Get a specific device by serial number.</summary>
    [HttpGet("{serial}")]
    public async Task<ActionResult<AndroidDevice>> GetDevice(string serial, CancellationToken ct)
    {
        var device = _monitor.GetCurrentDevices().FirstOrDefault(d => d.SerialNumber == serial);
        if (device == null)
        {
            return NotFound(ApiResult.Fail($"Device {serial} not found"));
        }

        // Refresh details
        try
        {
            var details = await _adbService.GetDeviceDetailsAsync(serial, ct);
            return Ok(details);
        }
        catch
        {
            return Ok(device);
        }
    }

    /// <summary>Force a device rescan.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<IReadOnlyList<AndroidDevice>>> RefreshDevices(CancellationToken ct)
    {
        await _monitor.RefreshNowAsync(ct);
        var devices = _monitor.GetCurrentDevices();
        await _hubContext.Clients.All.SendAsync("DevicesUpdated", devices, ct);
        return Ok(devices);
    }

    /// <summary>Reconnect a device via ADB.</summary>
    [HttpPost("{serial}/reconnect")]
    public async Task<ActionResult<ApiResult>> ReconnectDevice(string serial, CancellationToken ct)
    {
        var result = await _adbService.ReconnectDeviceAsync(serial, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"Reconnected device {serial}", "DeviceAction", serial, ct);
            return Ok(ApiResult.Ok("Device reconnected"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Reboot a device.</summary>
    [HttpPost("{serial}/reboot")]
    public async Task<ActionResult<ApiResult>> RebootDevice(string serial, CancellationToken ct)
    {
        var result = await _adbService.RebootDeviceAsync(serial, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"Rebooted device {serial}", "DeviceAction", serial, ct);
            return Ok(ApiResult.Ok("Device rebooting"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Capture a screenshot from a device.</summary>
    [HttpPost("{serial}/screenshot")]
    public async Task<ActionResult<ApiResult>> CaptureScreenshot(string serial, CancellationToken ct)
    {
        var savePath = Path.Combine(Path.GetTempPath(), $"mch_screenshot_{serial}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        var result = await _adbService.CaptureScreenshotAsync(serial, savePath, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"Screenshot captured for {serial}: {savePath}", "DeviceAction", serial, ct);
            return Ok(ApiResult<string>.Ok(savePath, "Screenshot captured"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Install an APK on a device.</summary>
    [HttpPost("{serial}/install")]
    public async Task<ActionResult<ApiResult>> InstallApk(string serial, [FromBody] InstallApkRequest request, CancellationToken ct)
    {
        var result = await _adbService.InstallApkAsync(serial, request.ApkPath, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"APK installed on {serial}: {request.ApkPath}", "DeviceAction", serial, ct);
            return Ok(ApiResult.Ok("APK installed"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Push a file to a device.</summary>
    [HttpPost("{serial}/push")]
    public async Task<ActionResult<ApiResult>> PushFile(string serial, [FromBody] PushFileRequest request, CancellationToken ct)
    {
        var result = await _adbService.PushFileAsync(serial, request.LocalPath, request.RemotePath, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"File pushed to {serial}: {request.LocalPath} -> {request.RemotePath}", "DeviceAction", serial, ct);
            return Ok(ApiResult.Ok("File pushed"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Pull a file from a device.</summary>
    [HttpPost("{serial}/pull")]
    public async Task<ActionResult<ApiResult>> PullFile(string serial, [FromBody] PullFileRequest request, CancellationToken ct)
    {
        var result = await _adbService.PullFileAsync(serial, request.RemotePath, request.LocalPath, ct);
        if (result.Success)
        {
            await _logService.LogInfoAsync($"File pulled from {serial}: {request.RemotePath} -> {request.LocalPath}", "DeviceAction", serial, ct);
            return Ok(ApiResult.Ok("File pulled"));
        }
        return BadRequest(ApiResult.Fail(result.StandardError));
    }

    /// <summary>Execute a shell command on a device.</summary>
    [HttpPost("{serial}/shell")]
    public async Task<ActionResult<ApiResult<string>>> ExecuteShell(string serial, [FromBody] ShellCommandRequest request, CancellationToken ct)
    {
        var result = await _adbService.ExecuteShellCommandAsync(serial, request.Command, ct);
        if (result.Success)
        {
            return Ok(ApiResult<string>.Ok(result.StandardOutput, "Command executed"));
        }
        return BadRequest(ApiResult<string>.Fail(result.StandardError));
    }

    /// <summary>Set a friendly name for a device.</summary>
    [HttpPut("{serial}/name")]
    public async Task<ActionResult<ApiResult>> SetFriendlyName(string serial, [FromBody] SetFriendlyNameRequest request, CancellationToken ct)
    {
        await _configService.SetDeviceFriendlyNameAsync(serial, request.FriendlyName, ct);
        await _logService.LogInfoAsync($"Set friendly name for {serial}: {request.FriendlyName}", "DeviceAction", serial, ct);
        return Ok(ApiResult.Ok("Friendly name updated"));
    }
}
