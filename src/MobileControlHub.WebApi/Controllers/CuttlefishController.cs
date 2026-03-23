using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// REST API for managing Cuttlefish Android VMs.
/// Provides Genymotion-like cloud Android platform with full VM emulation:
/// WebRTC streaming, virtual modem, GPS, sensors, camera, battery, biometrics.
/// </summary>
[ApiController]
[Route("api/cuttlefish")]
public class CuttlefishController : ControllerBase
{
    private readonly ICuttlefishService _service;

    public CuttlefishController(ICuttlefishService service)
    {
        _service = service;
    }

    /// <summary>Get platform status and host health.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _service.GetStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>Get all available device profiles (Samsung, Pixel, etc.).</summary>
    [HttpGet("profiles")]
    public IActionResult GetProfiles()
    {
        return Ok(_service.GetProfiles());
    }

    /// <summary>Get all available Android images.</summary>
    [HttpGet("images")]
    public IActionResult GetImages()
    {
        return Ok(_service.GetImages());
    }

    /// <summary>List all Cuttlefish VMs.</summary>
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(CancellationToken ct)
    {
        var devices = await _service.GetDevicesAsync(ct);
        return Ok(devices);
    }

    /// <summary>Get a single device by ID.</summary>
    [HttpGet("devices/{deviceId}")]
    public async Task<IActionResult> GetDevice(string deviceId, CancellationToken ct)
    {
        var device = await _service.GetDeviceAsync(deviceId, ct);
        if (device == null) return NotFound(new { message = $"Device {deviceId} not found" });
        return Ok(device);
    }

    /// <summary>Create a new Cuttlefish Android VM.</summary>
    [HttpPost("devices/create")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateCuttlefishRequest request, CancellationToken ct)
    {
        var device = await _service.CreateDeviceAsync(request, ct);
        if (device == null)
            return BadRequest(new { success = false, message = "Failed to create Cuttlefish VM. Check host configuration and resources." });

        return Ok(new { success = true, message = $"Cuttlefish VM '{device.Name}' created", device });
    }

    /// <summary>Stop and remove a Cuttlefish VM.</summary>
    [HttpPost("devices/{deviceId}/remove")]
    public async Task<IActionResult> RemoveDevice(string deviceId, CancellationToken ct)
    {
        var result = await _service.RemoveDeviceAsync(deviceId, ct);
        return result
            ? Ok(new { success = true, message = $"Removed VM {deviceId}" })
            : BadRequest(new { success = false, message = $"Failed to remove VM {deviceId}" });
    }

    /// <summary>Restart a Cuttlefish VM.</summary>
    [HttpPost("devices/{deviceId}/restart")]
    public async Task<IActionResult> RestartDevice(string deviceId, CancellationToken ct)
    {
        var result = await _service.RestartDeviceAsync(deviceId, ct);
        return result
            ? Ok(new { success = true, message = $"Restarted VM {deviceId}" })
            : BadRequest(new { success = false, message = $"Failed to restart VM {deviceId}" });
    }

    /// <summary>Get WebRTC streaming URL for a device.</summary>
    [HttpGet("devices/{deviceId}/stream-url")]
    public async Task<IActionResult> GetStreamUrl(string deviceId, CancellationToken ct)
    {
        var url = await _service.GetStreamUrlAsync(deviceId, ct);
        if (url == null) return NotFound(new { message = "Stream not available" });
        return Ok(new { streamUrl = url });
    }

    /// <summary>Execute an ADB shell command on a device.</summary>
    [HttpPost("devices/{deviceId}/shell")]
    public async Task<IActionResult> ExecShell(string deviceId, [FromBody] ShellRequest request, CancellationToken ct)
    {
        var output = await _service.ExecShellAsync(deviceId, request.Command, ct);
        return Ok(new { success = output != null, output = output ?? "Command failed" });
    }

    /// <summary>Set GPS location (latitude/longitude).</summary>
    [HttpPost("devices/{deviceId}/gps")]
    public async Task<IActionResult> SetGps(string deviceId, [FromBody] GpsRequest request, CancellationToken ct)
    {
        var result = await _service.SetGpsLocationAsync(deviceId, request.Latitude, request.Longitude, ct);
        return result
            ? Ok(new { success = true, message = $"GPS set to {request.Latitude},{request.Longitude}" })
            : BadRequest(new { success = false, message = "Failed to set GPS location" });
    }

    /// <summary>Set battery level and status.</summary>
    [HttpPost("devices/{deviceId}/battery")]
    public async Task<IActionResult> SetBattery(string deviceId, [FromBody] BatteryRequest request, CancellationToken ct)
    {
        var result = await _service.SetBatteryAsync(deviceId, request.Level, request.Status, ct);
        return result
            ? Ok(new { success = true, message = $"Battery set to {request.Level}% ({request.Status})" })
            : BadRequest(new { success = false, message = "Failed to set battery" });
    }

    /// <summary>Set network mode (wifi, cellular, airplane, off).</summary>
    [HttpPost("devices/{deviceId}/network")]
    public async Task<IActionResult> SetNetwork(string deviceId, [FromBody] NetworkRequest request, CancellationToken ct)
    {
        var result = await _service.SetNetworkAsync(deviceId, request.Mode, ct);
        return result
            ? Ok(new { success = true, message = $"Network set to {request.Mode}" })
            : BadRequest(new { success = false, message = "Failed to set network mode" });
    }

    /// <summary>Rotate the device display.</summary>
    [HttpPost("devices/{deviceId}/rotate")]
    public async Task<IActionResult> RotateDisplay(string deviceId, [FromBody] RotateRequest request, CancellationToken ct)
    {
        var result = await _service.RotateDisplayAsync(deviceId, request.Orientation, ct);
        return result
            ? Ok(new { success = true, message = $"Display rotated to {request.Orientation}" })
            : BadRequest(new { success = false, message = "Failed to rotate display" });
    }

    // ─── Request DTOs ────────────────────────────────────────────────────
    public class ShellRequest
    {
        public string Command { get; set; } = string.Empty;
    }

    public class GpsRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class BatteryRequest
    {
        public int Level { get; set; } = 100;
        public string Status { get; set; } = "charging";
    }

    public class NetworkRequest
    {
        public string Mode { get; set; } = "wifi";
    }

    public class RotateRequest
    {
        public string Orientation { get; set; } = "portrait";
    }
}
