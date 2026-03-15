using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Helpers;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages virtual Android devices running as Redroid Docker containers.
/// Supports creating, removing, listing, connecting, and disconnecting devices.
/// When running on the VPS, containers are local; when running on a PC, connects remotely.
/// </summary>
[ApiController]
[Route("api/virtual-devices")]
public class VirtualDevicesController : ControllerBase
{
    private readonly IAdbService _adbService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly IDeviceMonitorService _monitor;

    private const int BaseAdbPort = 5555;
    private const string RedroidImage = "redroid/redroid:14.0.0-latest";

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

    /// <summary>Get all virtual devices (Redroid containers) and their status.</summary>
    [HttpGet]
    public async Task<ActionResult<List<VirtualDeviceInfo>>> GetVirtualDevices(CancellationToken ct)
    {
        var containers = await ListRedroidContainersAsync(ct);
        var devices = _monitor.GetCurrentDevices();
        var config = await _configService.LoadAsync(ct);
        var vpsHost = config.VpsConfig?.Host ?? "127.0.0.1";

        var result = new List<VirtualDeviceInfo>();
        foreach (var c in containers)
        {
            var localSerial = $"localhost:{c.AdbPort}";
            var remoteSerial = $"{vpsHost}:{c.AdbPort}";

            var device = devices.FirstOrDefault(d =>
                d.SerialNumber == localSerial || d.SerialNumber == remoteSerial);

            result.Add(new VirtualDeviceInfo
            {
                Host = "localhost",
                Port = c.AdbPort,
                Serial = device?.SerialNumber ?? localSerial,
                FriendlyName = device?.FriendlyName ?? c.Name,
                Connected = device != null && device.ConnectionState == Domain.Enums.DeviceConnectionState.Online,
                AndroidVersion = device?.AndroidVersion ?? "14",
                Model = device?.Model ?? "Redroid Virtual Android",
                ContainerName = c.ContainerName,
                ContainerStatus = c.Status,
            });
        }

        return Ok(result);
    }

    /// <summary>Create a new virtual Android device (Redroid container).</summary>
    [HttpPost("create")]
    public async Task<ActionResult<ApiResult>> CreateDevice([FromBody] CreateVirtualDeviceRequest request, CancellationToken ct)
    {
        var containers = await ListRedroidContainersAsync(ct);

        // Find next available port
        var usedPorts = containers.Select(c => c.AdbPort).ToHashSet();
        var adbPort = BaseAdbPort;
        while (usedPorts.Contains(adbPort)) adbPort++;

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? $"Cloud Android {containers.Count + 1}"
            : request.Name;
        var containerName = $"android{adbPort - BaseAdbPort + 1}";

        if (containers.Any(c => c.ContainerName == containerName))
            containerName = $"android-{adbPort}";

        var ram = Math.Max(1, request.RamGB > 0 ? request.RamGB : 3);
        var cpus = Math.Max(1, request.Cpus > 0 ? request.Cpus : 2);

        var dockerCmd = $"docker run -d --name {containerName} --privileged " +
            $"--memory={ram}g --cpus={cpus} " +
            $"-p {adbPort}:5555 " +
            $"--restart=always " +
            $"{RedroidImage} " +
            $"androidboot.redroid_gpu_mode=guest";

        var result = await ProcessRunner.RunAsync("/bin/bash", $"-c \"{dockerCmd}\"", timeoutMs: 60000, ct: ct);

        if (!result.Success)
        {
            await _logService.LogErrorAsync($"Failed to create virtual device: {result.StandardError}",
                source: "VirtualDevices");
            return BadRequest(ApiResult.Fail($"Failed to create container: {result.StandardError}"));
        }

        await Task.Delay(3000, ct);

        var connectResult = await _adbService.ConnectDeviceAsync("localhost", adbPort, ct);
        if (connectResult.Success)
            await _configService.SetDeviceFriendlyNameAsync($"localhost:{adbPort}", name, ct);

        await _monitor.RefreshNowAsync(ct);

        await _logService.LogInfoAsync($"Created virtual device '{name}' on port {adbPort} (container: {containerName})",
            category: "VirtualDevices");

        return Ok(ApiResult.Ok($"Created virtual device '{name}' on port {adbPort}"));
    }

    /// <summary>Remove a virtual Android device (stop and remove Docker container).</summary>
    [HttpPost("remove")]
    public async Task<ActionResult<ApiResult>> RemoveDevice([FromBody] RemoveVirtualDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ContainerName))
            return BadRequest(ApiResult.Fail("Container name is required"));

        var containers = await ListRedroidContainersAsync(ct);
        var container = containers.FirstOrDefault(c => c.ContainerName == request.ContainerName);
        if (container != null)
        {
            try { await _adbService.DisconnectDeviceAsync("localhost", container.AdbPort, ct); }
            catch { }
        }

        var stopResult = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"docker stop {request.ContainerName} && docker rm {request.ContainerName}\"",
            timeoutMs: 30000, ct: ct);

        if (!stopResult.Success)
        {
            await ProcessRunner.RunAsync("/bin/bash",
                $"-c \"docker rm -f {request.ContainerName}\"",
                timeoutMs: 15000, ct: ct);
        }

        await _monitor.RefreshNowAsync(ct);

        await _logService.LogInfoAsync($"Removed virtual device container '{request.ContainerName}'",
            category: "VirtualDevices");

        return Ok(ApiResult.Ok($"Removed virtual device '{request.ContainerName}'"));
    }

    /// <summary>Connect to all virtual devices via ADB.</summary>
    [HttpPost("connect-all")]
    public async Task<ActionResult<ApiResult>> ConnectAll(CancellationToken ct)
    {
        var containers = await ListRedroidContainersAsync(ct);
        var connected = 0;

        foreach (var c in containers)
        {
            try
            {
                var result = await _adbService.ConnectDeviceAsync("localhost", c.AdbPort, ct);
                if (result.Success && !result.StandardOutput.Contains("unable"))
                {
                    connected++;
                    await _configService.SetDeviceFriendlyNameAsync($"localhost:{c.AdbPort}", c.Name, ct);
                }
            }
            catch { }
        }

        await _monitor.RefreshNowAsync(ct);

        foreach (var device in _monitor.GetCurrentDevices())
        {
            if (device.SerialNumber.StartsWith("localhost:") || device.SerialNumber.Contains(":555"))
            {
                device.IsVirtual = true;
                device.ConnectionType = "Cloud (Virtual)";
            }
        }

        await _logService.LogInfoAsync(
            $"Connected {connected}/{containers.Count} virtual devices",
            category: "VirtualDevices");

        return Ok(ApiResult.Ok($"Connected {connected} virtual device(s)"));
    }

    /// <summary>Connect to a specific virtual device.</summary>
    [HttpPost("connect")]
    public async Task<ActionResult<ApiResult>> Connect([FromBody] ConnectVirtualDeviceRequest request, CancellationToken ct)
    {
        var host = string.IsNullOrEmpty(request.Host) ? "localhost" : request.Host;
        var result = await _adbService.ConnectDeviceAsync(host, request.Port, ct);

        if (result.Success && !result.StandardOutput.Contains("unable"))
        {
            if (!string.IsNullOrEmpty(request.FriendlyName))
                await _configService.SetDeviceFriendlyNameAsync($"{host}:{request.Port}", request.FriendlyName, ct);

            await _monitor.RefreshNowAsync(ct);
            return Ok(ApiResult.Ok($"Connected to {host}:{request.Port}"));
        }

        return BadRequest(ApiResult.Fail($"Failed to connect: {result.StandardOutput} {result.StandardError}"));
    }

    /// <summary>Disconnect a specific virtual device.</summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult<ApiResult>> Disconnect([FromBody] ConnectVirtualDeviceRequest request, CancellationToken ct)
    {
        var host = string.IsNullOrEmpty(request.Host) ? "localhost" : request.Host;
        await _adbService.DisconnectDeviceAsync(host, request.Port, ct);
        await _monitor.RefreshNowAsync(ct);

        return Ok(ApiResult.Ok($"Disconnected {host}:{request.Port}"));
    }

    /// <summary>Disconnect all virtual devices.</summary>
    [HttpPost("disconnect-all")]
    public async Task<ActionResult<ApiResult>> DisconnectAll(CancellationToken ct)
    {
        var containers = await ListRedroidContainersAsync(ct);

        foreach (var c in containers)
        {
            try { await _adbService.DisconnectDeviceAsync("localhost", c.AdbPort, ct); }
            catch { }
        }

        await _monitor.RefreshNowAsync(ct);
        return Ok(ApiResult.Ok("Disconnected all virtual devices"));
    }

    /// <summary>Restart a virtual device container.</summary>
    [HttpPost("restart")]
    public async Task<ActionResult<ApiResult>> RestartDevice([FromBody] RemoveVirtualDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ContainerName))
            return BadRequest(ApiResult.Fail("Container name is required"));

        var result = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"docker restart {request.ContainerName}\"",
            timeoutMs: 30000, ct: ct);

        if (!result.Success)
            return BadRequest(ApiResult.Fail($"Failed to restart: {result.StandardError}"));

        await Task.Delay(3000, ct);

        var containers = await ListRedroidContainersAsync(ct);
        var container = containers.FirstOrDefault(c => c.ContainerName == request.ContainerName);
        if (container != null)
            await _adbService.ConnectDeviceAsync("localhost", container.AdbPort, ct);

        await _monitor.RefreshNowAsync(ct);
        return Ok(ApiResult.Ok($"Restarted '{request.ContainerName}'"));
    }

    // --- Helpers ---

    private record RedroidContainer(string ContainerName, string Name, int AdbPort, string Status);

    private async Task<List<RedroidContainer>> ListRedroidContainersAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("/bin/bash",
            "-c \"docker ps -a --filter name=android --format '{{.Names}}|{{.Status}}|{{.Ports}}' 2>/dev/null\"",
            timeoutMs: 10000, ct: ct);

        var containers = new List<RedroidContainer>();
        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            return containers;

        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length < 3) continue;

            var containerName = parts[0].Trim();
            var status = parts[1].Trim();
            var ports = parts[2].Trim();

            var adbPort = ParseAdbPort(ports);
            if (adbPort <= 0) continue;

            if (!containerName.StartsWith("android")) continue;

            var deviceNum = adbPort - BaseAdbPort + 1;
            var friendlyName = $"Cloud Android {deviceNum}";

            containers.Add(new RedroidContainer(containerName, friendlyName, adbPort, status));
        }

        return containers.OrderBy(c => c.AdbPort).ToList();
    }

    private static int ParseAdbPort(string ports)
    {
        foreach (var mapping in ports.Split(','))
        {
            var trimmed = mapping.Trim();
            if (trimmed.Contains("->5555/tcp"))
            {
                var colonIdx = trimmed.IndexOf(':');
                var arrowIdx = trimmed.IndexOf("->");
                if (colonIdx >= 0 && arrowIdx > colonIdx)
                {
                    var portStr = trimmed.Substring(colonIdx + 1, arrowIdx - colonIdx - 1);
                    if (int.TryParse(portStr, out var port))
                        return port;
                }
            }
        }
        return 0;
    }
}
