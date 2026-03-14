using MobileControlHub.Domain.Enums;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Domain.Models;
using MobileControlHub.Infrastructure.Helpers;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Implementation of IAdbService wrapping adb.exe commands.
/// </summary>
public class AdbService : IAdbService
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private string _adbPath = "adb";

    public AdbService(IConfigurationService configService, ILogService logService)
    {
        _configService = configService;
        _logService = logService;
    }

    private async Task<string> GetAdbPathAsync()
    {
        var config = await _configService.LoadAsync();
        _adbPath = config.AdbPath;
        return _adbPath;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var adbPath = await GetAdbPathAsync();
            if (ProcessRunner.ExecutableExists(adbPath))
            {
                var result = await ProcessRunner.RunAsync(adbPath, "version", timeoutMs: 5000, ct: ct);
                return result.Success;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var result = await ProcessRunner.RunAsync(adbPath, "version", timeoutMs: 5000, ct: ct);
        if (result.Success)
        {
            // Extract version from first line
            var firstLine = result.StandardOutput.Split('\n').FirstOrDefault() ?? "";
            return firstLine.Trim();
        }
        return "Unknown";
    }

    public async Task<List<AndroidDevice>> GetConnectedDevicesAsync(CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var result = await ProcessRunner.RunAsync(adbPath, "devices -l", timeoutMs: 10000, ct: ct);

        if (!result.Success)
        {
            await _logService.LogErrorAsync($"ADB devices command failed: {result.StandardError}",
                source: "AdbService");
            return new List<AndroidDevice>();
        }

        var devices = AdbOutputParser.ParseDeviceList(result.StandardOutput);
        await _logService.LogInfoAsync($"Found {devices.Count} device(s)", category: "ADB");
        return devices;
    }

    public async Task<AndroidDevice> GetDeviceDetailsAsync(string serial, CancellationToken ct = default)
    {
        var device = new AndroidDevice { SerialNumber = serial };
        var adbPath = await GetAdbPathAsync();

        try
        {
            // Get model
            var modelResult = await ProcessRunner.RunAsync(adbPath,
                $"-s {serial} shell getprop ro.product.model", timeoutMs: 5000, ct: ct);
            if (modelResult.Success)
                device.Model = modelResult.StandardOutput.Trim();

            // Get manufacturer
            var mfgResult = await ProcessRunner.RunAsync(adbPath,
                $"-s {serial} shell getprop ro.product.manufacturer", timeoutMs: 5000, ct: ct);
            if (mfgResult.Success)
                device.Manufacturer = mfgResult.StandardOutput.Trim();

            // Get Android version
            var verResult = await ProcessRunner.RunAsync(adbPath,
                $"-s {serial} shell getprop ro.build.version.release", timeoutMs: 5000, ct: ct);
            if (verResult.Success)
                device.AndroidVersion = verResult.StandardOutput.Trim();

            // Get battery level
            device.BatteryLevel = await GetBatteryLevelAsync(serial, ct);

            // Get screen state
            device.IsScreenOn = await IsScreenOnAsync(serial, ct);

            // Get friendly name from config
            var friendlyName = await _configService.GetDeviceFriendlyNameAsync(serial);
            if (!string.IsNullOrEmpty(friendlyName))
                device.FriendlyName = friendlyName;

            device.ConnectionState = DeviceConnectionState.Online;
            device.LastSeen = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            await _logService.LogErrorAsync($"Failed to get details for {serial}: {ex.Message}",
                deviceSerial: serial, source: "AdbService");
        }

        return device;
    }

    public async Task<int> GetBatteryLevelAsync(string serial, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var result = await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} shell dumpsys battery", timeoutMs: 5000, ct: ct);

        return result.Success ? AdbOutputParser.ParseBatteryLevel(result.StandardOutput) : -1;
    }

    public async Task<bool> IsScreenOnAsync(string serial, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var result = await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} shell dumpsys power", timeoutMs: 5000, ct: ct);

        return result.Success && AdbOutputParser.ParseScreenState(result.StandardOutput);
    }

    public async Task<CommandResult> ExecuteShellCommandAsync(string serial, string command, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var result = await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} shell {command}", timeoutMs: 30000, ct: ct);

        await _logService.LogInfoAsync($"Shell command on {serial}: {command}",
            category: "ADB", deviceSerial: serial);
        return result;
    }

    public async Task<CommandResult> InstallApkAsync(string serial, string apkPath, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogInfoAsync($"Installing APK on {serial}: {apkPath}",
            category: "ADB", deviceSerial: serial);

        return await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} install -r \"{apkPath}\"", timeoutMs: 120000, ct: ct);
    }

    public async Task<CommandResult> PushFileAsync(string serial, string localPath, string remotePath, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogInfoAsync($"Pushing file to {serial}: {localPath} -> {remotePath}",
            category: "ADB", deviceSerial: serial);

        return await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} push \"{localPath}\" \"{remotePath}\"", timeoutMs: 120000, ct: ct);
    }

    public async Task<CommandResult> PullFileAsync(string serial, string remotePath, string localPath, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogInfoAsync($"Pulling file from {serial}: {remotePath} -> {localPath}",
            category: "ADB", deviceSerial: serial);

        return await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} pull \"{remotePath}\" \"{localPath}\"", timeoutMs: 120000, ct: ct);
    }

    public async Task<CommandResult> CaptureScreenshotAsync(string serial, string savePath, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        var remotePath = "/sdcard/screenshot_mch.png";

        // Capture on device
        var captureResult = await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} shell screencap -p {remotePath}", timeoutMs: 10000, ct: ct);

        if (!captureResult.Success)
            return captureResult;

        // Pull to local
        var pullResult = await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} pull {remotePath} \"{savePath}\"", timeoutMs: 15000, ct: ct);

        // Clean up remote file
        await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} shell rm {remotePath}", timeoutMs: 5000, ct: ct);

        await _logService.LogInfoAsync($"Screenshot captured for {serial}: {savePath}",
            category: "ADB", deviceSerial: serial);

        return pullResult;
    }

    public async Task<CommandResult> RebootDeviceAsync(string serial, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogWarningAsync($"Rebooting device {serial}",
            category: "ADB", deviceSerial: serial);

        return await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} reboot", timeoutMs: 15000, ct: ct);
    }

    public async Task<CommandResult> ReconnectDeviceAsync(string serial, CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogInfoAsync($"Reconnecting device {serial}",
            category: "ADB", deviceSerial: serial);

        return await ProcessRunner.RunAsync(adbPath,
            $"-s {serial} reconnect", timeoutMs: 10000, ct: ct);
    }

    public async Task<CommandResult> StartServerAsync(CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogInfoAsync("Starting ADB server", category: "ADB");
        return await ProcessRunner.RunAsync(adbPath, "start-server", timeoutMs: 10000, ct: ct);
    }

    public async Task<CommandResult> KillServerAsync(CancellationToken ct = default)
    {
        var adbPath = await GetAdbPathAsync();
        await _logService.LogWarningAsync("Killing ADB server", category: "ADB");
        return await ProcessRunner.RunAsync(adbPath, "kill-server", timeoutMs: 10000, ct: ct);
    }
}
