using System.Collections.Concurrent;
using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Background monitoring service that tracks device connections,
/// refreshes device state, and detects ADB server failures.
/// </summary>
public class DeviceMonitorService : IDeviceMonitorService
{
    private readonly IAdbService _adbService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private readonly ConcurrentDictionary<string, AndroidDevice> _devices = new();

    public bool IsRunning => _monitorTask != null && !_monitorTask.IsCompleted;

    public event EventHandler<List<AndroidDevice>>? DevicesChanged;
    public event EventHandler<AndroidDevice>? DeviceConnected;
    public event EventHandler<AndroidDevice>? DeviceDisconnected;
    public event EventHandler<string>? AdbError;

    public DeviceMonitorService(
        IAdbService adbService,
        IScrcpyService scrcpyService,
        IConfigurationService configService,
        ILogService logService)
    {
        _adbService = adbService;
        _scrcpyService = scrcpyService;
        _configService = configService;
        _logService = logService;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _monitorTask = MonitorLoopAsync(_cts.Token);

        _logService.LogInfoAsync("Device monitor started", category: "Monitor");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_monitorTask != null)
            {
                try { await _monitorTask; } catch (OperationCanceledException) { }
            }
            _cts.Dispose();
            _cts = null;
        }

        await _logService.LogInfoAsync("Device monitor stopped", category: "Monitor");
    }

    public async Task RefreshNowAsync(CancellationToken ct = default)
    {
        await PollDevicesAsync(ct);
    }

    public IReadOnlyList<AndroidDevice> GetCurrentDevices()
    {
        return _devices.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Main monitoring loop that polls devices at configured intervals.
    /// </summary>
    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        int consecutiveFailures = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var config = await _configService.LoadAsync(ct);
                var interval = TimeSpan.FromSeconds(config.DevicePollIntervalSeconds);

                await PollDevicesAsync(ct);
                consecutiveFailures = 0;

                // Check scrcpy session health
                CheckScrcpySessions();

                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                await _logService.LogErrorAsync(
                    $"Monitor error (attempt {consecutiveFailures}): {ex.Message}",
                    source: "DeviceMonitor");

                if (consecutiveFailures >= 3)
                {
                    AdbError?.Invoke(this, "ADB server may be unresponsive. Attempting recovery...");
                    await TryRecoverAdbAsync(ct);
                }

                // Exponential backoff on failures
                var delay = Math.Min(consecutiveFailures * 2000, 30000);
                await Task.Delay(delay, ct);
            }
        }
    }

    /// <summary>
    /// Poll connected devices and detect changes.
    /// </summary>
    private async Task PollDevicesAsync(CancellationToken ct)
    {
        var discovered = await _adbService.GetConnectedDevicesAsync(ct);
        var discoveredSerials = discovered.Select(d => d.SerialNumber).ToHashSet();
        var previousSerials = _devices.Keys.ToHashSet();
        bool changed = false;

        // Detect new devices
        foreach (var device in discovered)
        {
            if (!previousSerials.Contains(device.SerialNumber))
            {
                // New device -- get full details
                var detailed = await _adbService.GetDeviceDetailsAsync(device.SerialNumber, ct);
                detailed.ConnectionState = device.ConnectionState;

                // Mark virtual/cloud devices (TCP/IP connections to VPS)
                if (device.SerialNumber.Contains(':'))
                {
                    detailed.ConnectionType = "Cloud (TCP/IP)";
                    detailed.IsVirtual = true;
                    var hostPart = device.SerialNumber.Split(':')[0];
                    detailed.VpsHost = hostPart;
                }

                // Apply friendly name if available
                var name = await _configService.GetDeviceFriendlyNameAsync(device.SerialNumber);
                if (!string.IsNullOrEmpty(name))
                    detailed.FriendlyName = name;

                // Check for active scrcpy session
                var session = _scrcpyService.GetSessionForDevice(device.SerialNumber);
                detailed.HasActiveSession = session?.State == SessionState.Running;

                _devices[device.SerialNumber] = detailed;
                DeviceConnected?.Invoke(this, detailed);

                await _logService.LogInfoAsync(
                    $"Device connected: {detailed.DisplayName} ({device.SerialNumber})",
                    category: "Monitor", deviceSerial: device.SerialNumber);

                changed = true;
            }
            else
            {
                // Existing device -- update state
                if (_devices.TryGetValue(device.SerialNumber, out var existing))
                {
                    var oldState = existing.ConnectionState;
                    existing.ConnectionState = device.ConnectionState;
                    existing.LastSeen = DateTime.UtcNow;

                    // Refresh battery periodically
                    if (device.ConnectionState == DeviceConnectionState.Online)
                    {
                        existing.BatteryLevel = await _adbService.GetBatteryLevelAsync(device.SerialNumber, ct);
                    }

                    // Update scrcpy session status
                    var session = _scrcpyService.GetSessionForDevice(device.SerialNumber);
                    existing.HasActiveSession = session?.State == SessionState.Running;

                    if (oldState != device.ConnectionState)
                        changed = true;
                }
            }
        }

        // Detect disconnected devices
        foreach (var serial in previousSerials.Except(discoveredSerials))
        {
            if (_devices.TryRemove(serial, out var removed))
            {
                removed.ConnectionState = DeviceConnectionState.Disconnected;
                DeviceDisconnected?.Invoke(this, removed);

                await _logService.LogWarningAsync(
                    $"Device disconnected: {removed.DisplayName} ({serial})",
                    category: "Monitor", deviceSerial: serial);

                changed = true;
            }
        }

        if (changed)
        {
            DevicesChanged?.Invoke(this, _devices.Values.ToList());
        }
    }

    /// <summary>
    /// Check health of scrcpy sessions and update device states.
    /// </summary>
    private void CheckScrcpySessions()
    {
        foreach (var session in _scrcpyService.GetActiveSessions())
        {
            if (!_scrcpyService.IsSessionAlive(session.SessionId))
            {
                // Session died -- update device state
                if (_devices.TryGetValue(session.DeviceSerial, out var device))
                {
                    device.HasActiveSession = false;
                }
            }
        }
    }

    /// <summary>
    /// Attempt to recover from ADB server failures.
    /// </summary>
    private async Task TryRecoverAdbAsync(CancellationToken ct)
    {
        try
        {
            await _logService.LogWarningAsync("Attempting ADB server recovery...", category: "Monitor");
            await _adbService.KillServerAsync(ct);
            await Task.Delay(1000, ct);
            await _adbService.StartServerAsync(ct);
            await _logService.LogInfoAsync("ADB server restarted successfully", category: "Monitor");
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync($"ADB recovery failed: {ex.Message}", source: "DeviceMonitor");
        }
    }
}
