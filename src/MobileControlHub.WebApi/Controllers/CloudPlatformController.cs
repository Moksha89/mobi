using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages self-hosted cloud Android devices on Kubernetes.
/// Provides hardware profiles, OS images, device lifecycle, and WebRTC streaming.
/// </summary>
[ApiController]
[Route("api/cloud-platform")]
public class CloudPlatformController : ControllerBase
{
    private readonly ICloudPlatformService _cloudService;
    private readonly ILogService _logService;

    public CloudPlatformController(ICloudPlatformService cloudService, ILogService logService)
    {
        _cloudService = cloudService;
        _logService = logService;
    }

    /// <summary>Get platform status and cluster health.</summary>
    [HttpGet("status")]
    public async Task<ActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _cloudService.GetStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>Get all available hardware profiles (Samsung, Pixel, etc.).</summary>
    [HttpGet("profiles")]
    public ActionResult GetProfiles()
    {
        return Ok(_cloudService.GetHardwareProfiles());
    }

    /// <summary>Get all available OS images (Android 13-15).</summary>
    [HttpGet("images")]
    public ActionResult GetImages()
    {
        return Ok(_cloudService.GetOsImages());
    }

    /// <summary>List all running cloud devices.</summary>
    [HttpGet("devices")]
    public async Task<ActionResult> GetDevices(CancellationToken ct)
    {
        var devices = await _cloudService.GetDevicesAsync(ct);
        return Ok(devices);
    }

    /// <summary>Get a single device by ID.</summary>
    [HttpGet("devices/{deviceId}")]
    public async Task<ActionResult> GetDevice(string deviceId, CancellationToken ct)
    {
        var device = await _cloudService.GetDeviceAsync(deviceId, ct);
        if (device == null)
            return NotFound(ApiResult.Fail($"Device '{deviceId}' not found"));
        return Ok(device);
    }

    /// <summary>Create a new cloud Android device.</summary>
    [HttpPost("devices/create")]
    public async Task<ActionResult> CreateDevice([FromBody] CreateCloudDeviceApiRequest request, CancellationToken ct)
    {
        if (!_cloudService.IsConfigured)
            return BadRequest(ApiResult.Fail("Cloud platform not configured. Set MCH_K8S_NODE_HOST environment variable."));

        var domainRequest = new CreateCloudDeviceRequest
        {
            Name = request.Name,
            HardwareProfileId = request.HardwareProfileId,
            OsImageId = request.OsImageId,
            AssignPhoneNumber = request.AssignPhoneNumber,
            PersistentStorage = request.PersistentStorage,
            EnableGpu = request.EnableGpu,
        };

        var device = await _cloudService.CreateDeviceAsync(domainRequest, ct);
        if (device == null)
            return BadRequest(ApiResult.Fail("Failed to create cloud device. Check hardware profile and OS image IDs."));

        await _logService.LogInfoAsync(
            $"Created cloud device '{device.Name}' ({device.HardwareProfileName}, Android {device.AndroidVersion})",
            category: "CloudPlatform");

        return Ok(new { success = true, message = $"Created '{device.Name}'", device });
    }

    /// <summary>Remove a cloud device.</summary>
    [HttpPost("devices/{deviceId}/remove")]
    public async Task<ActionResult> RemoveDevice(string deviceId, CancellationToken ct)
    {
        if (!_cloudService.IsConfigured)
            return BadRequest(ApiResult.Fail("Cloud platform not configured."));

        var device = await _cloudService.GetDeviceAsync(deviceId, ct);
        var success = await _cloudService.RemoveDeviceAsync(deviceId, ct);

        if (!success)
            return BadRequest(ApiResult.Fail($"Failed to remove device '{deviceId}'"));

        await _logService.LogInfoAsync(
            $"Removed cloud device '{device?.Name ?? deviceId}'",
            category: "CloudPlatform");

        return Ok(ApiResult.Ok($"Removed device '{device?.Name ?? deviceId}'"));
    }

    /// <summary>Restart a cloud device.</summary>
    [HttpPost("devices/{deviceId}/restart")]
    public async Task<ActionResult> RestartDevice(string deviceId, CancellationToken ct)
    {
        if (!_cloudService.IsConfigured)
            return BadRequest(ApiResult.Fail("Cloud platform not configured."));

        var success = await _cloudService.RestartDeviceAsync(deviceId, ct);
        if (!success)
            return BadRequest(ApiResult.Fail($"Failed to restart device '{deviceId}'"));

        return Ok(ApiResult.Ok($"Restarted device '{deviceId}'"));
    }

    /// <summary>Get WebRTC streaming URL for a device.</summary>
    [HttpGet("devices/{deviceId}/stream-url")]
    public async Task<ActionResult> GetStreamUrl(string deviceId, CancellationToken ct)
    {
        var url = await _cloudService.GetStreamUrlAsync(deviceId, ct);
        if (url == null)
            return BadRequest(ApiResult.Fail("Device not found or not running"));

        return Ok(new { streamUrl = url });
    }

    /// <summary>Execute an ADB shell command on a device.</summary>
    [HttpPost("devices/{deviceId}/shell")]
    public async Task<ActionResult> ExecShell(string deviceId, [FromBody] ShellCommandRequest request, CancellationToken ct)
    {
        if (!_cloudService.IsConfigured)
            return BadRequest(ApiResult.Fail("Cloud platform not configured."));

        var output = await _cloudService.ExecShellAsync(deviceId, request.Command, ct);
        if (output == null)
            return BadRequest(ApiResult.Fail("Shell command failed"));

        return Ok(new { output });
    }

    /// <summary>Get cluster resource usage.</summary>
    [HttpGet("resources")]
    public async Task<ActionResult> GetResources(CancellationToken ct)
    {
        var resources = await _cloudService.GetClusterResourcesAsync(ct);
        return Ok(resources);
    }
}

/// <summary>Request to create a cloud device via API.</summary>
public class CreateCloudDeviceApiRequest
{
    public string Name { get; set; } = string.Empty;
    public string HardwareProfileId { get; set; } = string.Empty;
    public string OsImageId { get; set; } = string.Empty;
    public bool AssignPhoneNumber { get; set; } = true;
    public bool PersistentStorage { get; set; } = true;
    public bool EnableGpu { get; set; } = true;
}
