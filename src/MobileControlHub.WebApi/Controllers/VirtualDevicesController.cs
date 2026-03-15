using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages virtual Android devices running on the VPS (Redroid containers).
/// These are always-on cloud Android devices accessible via ADB over TCP/IP.
/// </summary>
[ApiController]
[Route("api/virtual-devices")]
public class VirtualDevicesController : ControllerBase
{
    private readonly IAdbService _adbService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly IDeviceMonitorService _monitor;

    // Default VPS virtual device ports (Redroid containers)
    private static readonly (int Port, string Name)[] DefaultVirtualDevices = new[]
    {
        (5555, "Cloud Android 1"),
        (5556, "Cloud Android 2"),
        (5557, "Cloud Android 3"),
    };

    public VirtualDevicesController(
        IAdbService adbService,
        IConfigurationService configService,
        ILogService logService,
        IDeviceMonitorService monitor)
    {
        _adbService = adbService;
        _configService = configService;
        _logService = logService;
        _monitor = monitor;
    }

    /// <summary>Get all virtual devices and their connection status.</summary>
    [HttpGet]
    public async Task<ActionResult<List<VirtualDeviceInfo>>> GetVirtualDevices(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        var vpsHost = config.VpsConfig?.Host ?? "69.197.142.77";
        var devices = _monitor.GetCurrentDevices();

        var result = new List<VirtualDeviceInfo>();
        foreach (var (port, name) in DefaultVirtualDevices)
        {
            var serial = $"{vpsHost}:{port}";
            var device = devices.FirstOrDefault(d => d.SerialNumber == serial);
            result.Add(new VirtualDeviceInfo
            {
                Host = vpsHost,
                Port = port,
                Serial = serial,
                FriendlyName = device?.FriendlyName ?? name,
                Connected = device != null && device.ConnectionState == Domain.Enums.DeviceConnectionState.Online,
                AndroidVersion = device?.AndroidVersion ?? "",
                Model = device?.Model ?? "Redroid Virtual Android",
            });
        }

        return Ok(result);
    }

    /// <summary>Connect to all virtual devices on the VPS.</summary>
    [HttpPost("connect-all")]
    public async Task<ActionResult<ApiResult>> ConnectAll(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        var vpsHost = config.VpsConfig?.Host ?? "69.197.142.77";
        var connected = 0;

        foreach (var (port, name) in DefaultVirtualDevices)
        {
            try
            {
                var result = await _adbService.ConnectDeviceAsync(vpsHost, port, ct);
                if (result.Success && !result.StandardOutput.Contains("unable"))
                {
                    connected++;
                    // Set friendly name and mark as virtual
                    await _configService.SetDeviceFriendlyNameAsync($"{vpsHost}:{port}", name, ct);
                }
            }
            catch
            {
                // Continue connecting other devices
            }
        }

        // Refresh device list to pick up new connections
        await _monitor.RefreshNowAsync(ct);

        // Mark virtual devices
        foreach (var device in _monitor.GetCurrentDevices())
        {
            if (device.SerialNumber.StartsWith(vpsHost))
            {
                device.IsVirtual = true;
                device.VpsHost = vpsHost;
                device.ConnectionType = "Cloud (TCP/IP)";
            }
        }

        await _logService.LogInfoAsync(
            $"Connected {connected}/{DefaultVirtualDevices.Length} virtual devices on {vpsHost}",
            category: "VirtualDevices");

        return Ok(ApiResult.Ok($"Connected {connected} virtual device(s)"));
    }

    /// <summary>Connect to a specific virtual device.</summary>
    [HttpPost("connect")]
    public async Task<ActionResult<ApiResult>> Connect([FromBody] ConnectVirtualDeviceRequest request, CancellationToken ct)
    {
        var host = string.IsNullOrEmpty(request.Host) ? "69.197.142.77" : request.Host;
        var result = await _adbService.ConnectDeviceAsync(host, request.Port, ct);

        if (result.Success && !result.StandardOutput.Contains("unable"))
        {
            if (!string.IsNullOrEmpty(request.FriendlyName))
            {
                await _configService.SetDeviceFriendlyNameAsync($"{host}:{request.Port}", request.FriendlyName, ct);
            }

            await _monitor.RefreshNowAsync(ct);
            return Ok(ApiResult.Ok($"Connected to {host}:{request.Port}"));
        }

        return BadRequest(ApiResult.Fail($"Failed to connect: {result.StandardOutput} {result.StandardError}"));
    }

    /// <summary>Disconnect a specific virtual device.</summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult<ApiResult>> Disconnect([FromBody] ConnectVirtualDeviceRequest request, CancellationToken ct)
    {
        var host = string.IsNullOrEmpty(request.Host) ? "69.197.142.77" : request.Host;
        var result = await _adbService.DisconnectDeviceAsync(host, request.Port, ct);
        await _monitor.RefreshNowAsync(ct);

        return Ok(ApiResult.Ok($"Disconnected {host}:{request.Port}"));
    }

    /// <summary>Disconnect all virtual devices.</summary>
    [HttpPost("disconnect-all")]
    public async Task<ActionResult<ApiResult>> DisconnectAll(CancellationToken ct)
    {
        var config = await _configService.LoadAsync(ct);
        var vpsHost = config.VpsConfig?.Host ?? "69.197.142.77";

        foreach (var (port, _) in DefaultVirtualDevices)
        {
            try
            {
                await _adbService.DisconnectDeviceAsync(vpsHost, port, ct);
            }
            catch { }
        }

        await _monitor.RefreshNowAsync(ct);
        return Ok(ApiResult.Ok("Disconnected all virtual devices"));
    }
}
