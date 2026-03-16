using Microsoft.AspNetCore.Mvc;
using MobileControlHub.Infrastructure.Helpers;
using MobileControlHub.WebApi.Models;

namespace MobileControlHub.WebApi.Controllers;

/// <summary>
/// Manages remote physical devices bridged from a PC via SSH tunnel.
/// The PC's ADB server port (5037) is forwarded to VPS port 15037 via the tunnel.
/// This controller queries that remote ADB server to discover physical USB devices.
/// </summary>
[ApiController]
[Route("api/remote-devices")]
public class RemoteDevicesController : ControllerBase
{
    private static readonly List<RegisterRemoteAdbRequest> _remoteHosts = new()
    {
        new RegisterRemoteAdbRequest { Host = "127.0.0.1", Port = 15037, Label = "PC" }
    };

    private static readonly List<RemotePhysicalDevice> _cachedDevices = new();
    private static readonly List<RemotePhysicalDevice> _pushedDevices = new();
    private static DateTime _lastScan = DateTime.MinValue;
    private static DateTime _lastPush = DateTime.MinValue;

    /// <summary>Check if a device serial exists in the pushed device list (used by ScreenController for proxying).</summary>
    public static bool IsPushedDevice(string serial)
    {
        lock (_pushedDevices)
        {
            if ((DateTime.UtcNow - _lastPush).TotalSeconds > 30)
                return false;
            return _pushedDevices.Any(d => d.Serial == serial);
        }
    }

    /// <summary>Get all remote physical devices (from ADB bridge + pushed from PC).</summary>
    [HttpGet]
    public async Task<ActionResult<List<RemotePhysicalDevice>>> GetRemoteDevices(CancellationToken ct)
    {
        // Re-scan ADB bridge if cache is older than 5 seconds
        if ((DateTime.UtcNow - _lastScan).TotalSeconds > 5)
        {
            await ScanRemoteDevicesAsync(ct);
        }

        // Combine ADB-scanned devices + pushed devices (pushed devices expire after 30s)
        var combined = new List<RemotePhysicalDevice>(_cachedDevices);
        lock (_pushedDevices)
        {
            if ((DateTime.UtcNow - _lastPush).TotalSeconds < 30)
            {
                // Add pushed devices that aren't already in the ADB-scanned list
                foreach (var pd in _pushedDevices)
                {
                    if (!combined.Any(d => d.Serial == pd.Serial))
                        combined.Add(pd);
                }
            }
        }
        return Ok(combined);
    }

    /// <summary>Push device list from PC to VPS (called by PC's background sync job).</summary>
    [HttpPost("push")]
    public ActionResult<ApiResult> PushDevices([FromBody] List<RemotePhysicalDevice> devices)
    {
        lock (_pushedDevices)
        {
            _pushedDevices.Clear();
            _pushedDevices.AddRange(devices);
            _lastPush = DateTime.UtcNow;
        }
        return Ok(ApiResult.Ok($"Received {devices.Count} device(s) from PC"));
    }

    /// <summary>Force rescan of remote physical devices.</summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<List<RemotePhysicalDevice>>> RefreshRemoteDevices(CancellationToken ct)
    {
        await ScanRemoteDevicesAsync(ct);
        return Ok(_cachedDevices.ToList());
    }

    /// <summary>Get registered remote ADB hosts.</summary>
    [HttpGet("hosts")]
    public ActionResult<List<RegisterRemoteAdbRequest>> GetRemoteHosts()
    {
        return Ok(_remoteHosts.ToList());
    }

    /// <summary>Register a new remote ADB host.</summary>
    [HttpPost("hosts/register")]
    public ActionResult<ApiResult> RegisterHost([FromBody] RegisterRemoteAdbRequest request)
    {
        if (_remoteHosts.Any(h => h.Host == request.Host && h.Port == request.Port))
            return Ok(ApiResult.Ok("Host already registered"));

        _remoteHosts.Add(request);
        return Ok(ApiResult.Ok($"Registered remote ADB host {request.Label} at {request.Host}:{request.Port}"));
    }

    /// <summary>Remove a remote ADB host.</summary>
    [HttpPost("hosts/remove")]
    public ActionResult<ApiResult> RemoveHost([FromBody] RegisterRemoteAdbRequest request)
    {
        _remoteHosts.RemoveAll(h => h.Host == request.Host && h.Port == request.Port);
        _cachedDevices.RemoveAll(d => d.Source == $"remote-{request.Label.ToLowerInvariant()}");
        return Ok(ApiResult.Ok("Host removed"));
    }

    /// <summary>Check if the remote ADB bridge is connected (tunnel active) and push status.</summary>
    [HttpGet("status")]
    public async Task<ActionResult> GetBridgeStatus(CancellationToken ct)
    {
        var results = new List<object>();
        foreach (var host in _remoteHosts)
        {
            var reachable = await CheckAdbHostReachableAsync(host.Host, host.Port, ct);
            results.Add(new
            {
                host.Host,
                host.Port,
                host.Label,
                IsReachable = reachable,
                DeviceCount = _cachedDevices.Count(d => d.Source == $"remote-{host.Label.ToLowerInvariant()}")
            });
        }

        var pushActive = (DateTime.UtcNow - _lastPush).TotalSeconds < 30;
        int pushedCount;
        lock (_pushedDevices) { pushedCount = _pushedDevices.Count; }

        return Ok(new
        {
            Hosts = results,
            TotalDevices = _cachedDevices.Count + (pushActive ? pushedCount : 0),
            LastScan = _lastScan,
            PushActive = pushActive,
            PushedDevices = pushedCount,
            LastPush = _lastPush
        });
    }

    private async Task ScanRemoteDevicesAsync(CancellationToken ct)
    {
        var allDevices = new List<RemotePhysicalDevice>();

        foreach (var host in _remoteHosts)
        {
            try
            {
                var devices = await ScanAdbHostAsync(host, ct);
                allDevices.AddRange(devices);
            }
            catch
            {
                // Host unreachable - skip
            }
        }

        lock (_cachedDevices)
        {
            _cachedDevices.Clear();
            _cachedDevices.AddRange(allDevices);
            _lastScan = DateTime.UtcNow;
        }
    }

    private async Task<List<RemotePhysicalDevice>> ScanAdbHostAsync(RegisterRemoteAdbRequest host, CancellationToken ct)
    {
        var devices = new List<RemotePhysicalDevice>();

        // Use ADB_SERVER_SOCKET env var to query the remote ADB server
        var envPrefix = $"ADB_SERVER_SOCKET=tcp:{host.Host}:{host.Port}";
        var result = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"{envPrefix} adb devices -l 2>/dev/null\"",
            timeoutMs: 8000, ct: ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            return devices;

        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("List of") || line.StartsWith("*"))
                continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var serial = parts[0].Trim();
            var state = parts[1].Trim();

            // Skip emulators and localhost connections (those are virtual devices)
            if (serial.StartsWith("emulator-") || serial.StartsWith("localhost:") || serial.StartsWith("127.0.0.1:"))
                continue;

            var device = new RemotePhysicalDevice
            {
                Serial = serial,
                ConnectionState = state,
                Source = $"remote-{host.Label.ToLowerInvariant()}",
            };

            // Parse extra info from -l output (e.g., "model:Pixel_8 device:shiba transport_id:1")
            for (int i = 2; i < parts.Length; i++)
            {
                var kv = parts[i].Split(':');
                if (kv.Length == 2)
                {
                    switch (kv[0])
                    {
                        case "model":
                            device.Model = kv[1].Replace('_', ' ');
                            break;
                        case "device":
                            device.FriendlyName = kv[1];
                            break;
                        case "transport_id":
                            device.TransportType = "usb";
                            break;
                    }
                }
            }

            // Try to get more details if device is online
            if (state == "device")
            {
                device.ConnectionState = "Online";
                await EnrichDeviceDetailsAsync(host, serial, device, ct);
            }
            else if (state == "unauthorized")
            {
                device.ConnectionState = "Unauthorized";
            }
            else if (state == "offline")
            {
                device.ConnectionState = "Offline";
            }

            devices.Add(device);
        }

        return devices;
    }

    private async Task EnrichDeviceDetailsAsync(RegisterRemoteAdbRequest host, string serial, RemotePhysicalDevice device, CancellationToken ct)
    {
        var envPrefix = $"ADB_SERVER_SOCKET=tcp:{host.Host}:{host.Port}";

        // Get manufacturer
        var mfgResult = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"{envPrefix} adb -s {serial} shell getprop ro.product.manufacturer 2>/dev/null\"",
            timeoutMs: 5000, ct: ct);
        if (mfgResult.Success && !string.IsNullOrWhiteSpace(mfgResult.StandardOutput))
            device.Manufacturer = mfgResult.StandardOutput.Trim();

        // Get model
        var modelResult = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"{envPrefix} adb -s {serial} shell getprop ro.product.model 2>/dev/null\"",
            timeoutMs: 5000, ct: ct);
        if (modelResult.Success && !string.IsNullOrWhiteSpace(modelResult.StandardOutput))
            device.Model = modelResult.StandardOutput.Trim();

        // Get Android version
        var verResult = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"{envPrefix} adb -s {serial} shell getprop ro.build.version.release 2>/dev/null\"",
            timeoutMs: 5000, ct: ct);
        if (verResult.Success && !string.IsNullOrWhiteSpace(verResult.StandardOutput))
            device.AndroidVersion = verResult.StandardOutput.Trim();

        // Get battery level
        var battResult = await ProcessRunner.RunAsync("/bin/bash",
            $"-c \"{envPrefix} adb -s {serial} shell dumpsys battery 2>/dev/null\"",
            timeoutMs: 5000, ct: ct);
        if (battResult.Success)
        {
            foreach (var bLine in battResult.StandardOutput.Split('\n'))
            {
                if (bLine.Trim().StartsWith("level:"))
                {
                    var levelStr = bLine.Split(':').Last().Trim();
                    if (int.TryParse(levelStr, out var level))
                        device.BatteryLevel = level;
                    break;
                }
            }
        }

        // Set friendly name from manufacturer + model
        if (!string.IsNullOrEmpty(device.Manufacturer) && !string.IsNullOrEmpty(device.Model))
        {
            var mfg = char.ToUpper(device.Manufacturer[0]) + device.Manufacturer[1..];
            device.FriendlyName = $"{mfg} {device.Model}";
        }
    }

    private async Task<bool> CheckAdbHostReachableAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            var envPrefix = $"ADB_SERVER_SOCKET=tcp:{host}:{port}";
            var result = await ProcessRunner.RunAsync("/bin/bash",
                $"-c \"{envPrefix} adb devices 2>/dev/null\"",
                timeoutMs: 5000, ct: ct);
            return result.Success && result.StandardOutput.Contains("List of devices");
        }
        catch
        {
            return false;
        }
    }
}
