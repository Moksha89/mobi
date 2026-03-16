using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Helpers;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Manages Cuttlefish-based cloud Android VMs using Docker containers.
/// Each VM runs a full QEMU/KVM Android instance with WebRTC streaming,
/// virtual modem, GPS, sensors, camera, battery, and biometrics.
/// This is the Genymotion-equivalent open-source platform.
/// </summary>
public class CuttlefishService : ICuttlefishService
{
    private readonly ILogger<CuttlefishService> _logger;
    private readonly ITwilioService _twilioService;
    private readonly string _hostAddress;
    private readonly bool _configured;

    // Port allocation ranges for Cuttlefish instances
    private const int AdbPortBase = 6520;
    private const int WebRtcPortBase = 8443;
    private const int ControlPortBase = 1443;
    private const int MaxDevices = 20;

    // Device state tracking
    private static readonly Dictionary<string, CuttlefishDeviceState> _deviceStates = new();
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public CuttlefishService(ILogger<CuttlefishService> logger, ITwilioService twilioService)
    {
        _logger = logger;
        _twilioService = twilioService;
        _hostAddress = Environment.GetEnvironmentVariable("MCH_CUTTLEFISH_HOST") ?? "";
        _configured = !string.IsNullOrEmpty(_hostAddress);
    }

    public bool IsConfigured => _configured;

    // ─── Device Profiles ────────────────────────────────────────────────
    private static readonly List<CuttlefishProfile> _profiles = new()
    {
        // Samsung Flagships
        new CuttlefishProfile
        {
            Id = "cf-samsung-s25-ultra",
            Name = "Samsung Galaxy S25 Ultra",
            Brand = "Samsung",
            Model = "SM-S938B",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 12288,
            StorageGb = 256,
            ScreenWidth = 1440,
            ScreenHeight = 3120,
            ScreenDpi = 505,
            GpuMode = "gfxstream",
            Description = "Samsung 2025 flagship with full VM emulation - modem, GPS, sensors, camera, biometrics",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new CuttlefishProfile
        {
            Id = "cf-samsung-s24-ultra",
            Name = "Samsung Galaxy S24 Ultra",
            Brand = "Samsung",
            Model = "SM-S928B",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 12288,
            StorageGb = 256,
            ScreenWidth = 1440,
            ScreenHeight = 3088,
            ScreenDpi = 501,
            GpuMode = "gfxstream",
            Description = "Samsung 2024 flagship with Snapdragon 8 Gen 3 emulation",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new CuttlefishProfile
        {
            Id = "cf-samsung-s23-ultra",
            Name = "Samsung Galaxy S23 Ultra",
            Brand = "Samsung",
            Model = "SM-S918B",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 8192,
            StorageGb = 256,
            ScreenWidth = 1440,
            ScreenHeight = 3088,
            ScreenDpi = 500,
            GpuMode = "gfxstream",
            Description = "Samsung 2023 flagship with S Pen emulation",
            SupportedAndroidVersions = new[] { "14", "13" },
        },
        // Google Pixel
        new CuttlefishProfile
        {
            Id = "cf-pixel-9-pro-xl",
            Name = "Google Pixel 9 Pro XL",
            Brand = "Google",
            Model = "Pixel 9 Pro XL",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 16384,
            StorageGb = 256,
            ScreenWidth = 1344,
            ScreenHeight = 2992,
            ScreenDpi = 486,
            GpuMode = "gfxstream",
            Description = "Google 2024 flagship - native Cuttlefish target, best compatibility",
            SupportedAndroidVersions = new[] { "15" },
        },
        new CuttlefishProfile
        {
            Id = "cf-pixel-9-pro",
            Name = "Google Pixel 9 Pro",
            Brand = "Google",
            Model = "Pixel 9 Pro",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 16384,
            StorageGb = 128,
            ScreenWidth = 1280,
            ScreenHeight = 2856,
            ScreenDpi = 495,
            GpuMode = "gfxstream",
            Description = "Google 2024 Pro - Tensor G4 emulation with Gemini AI",
            SupportedAndroidVersions = new[] { "15" },
        },
        new CuttlefishProfile
        {
            Id = "cf-pixel-9",
            Name = "Google Pixel 9",
            Brand = "Google",
            Model = "Pixel 9",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 12288,
            StorageGb = 128,
            ScreenWidth = 1080,
            ScreenHeight = 2424,
            ScreenDpi = 422,
            GpuMode = "gfxstream",
            Description = "Google 2024 standard - great balance of performance and resources",
            SupportedAndroidVersions = new[] { "15" },
        },
        new CuttlefishProfile
        {
            Id = "cf-pixel-8-pro",
            Name = "Google Pixel 8 Pro",
            Brand = "Google",
            Model = "Pixel 8 Pro",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 12288,
            StorageGb = 128,
            ScreenWidth = 1344,
            ScreenHeight = 2992,
            ScreenDpi = 489,
            GpuMode = "gfxstream",
            Description = "Google 2023 Pro with temperature sensor emulation",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        // OnePlus
        new CuttlefishProfile
        {
            Id = "cf-oneplus-12",
            Name = "OnePlus 12",
            Brand = "OnePlus",
            Model = "CPH2583",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 16384,
            StorageGb = 256,
            ScreenWidth = 1440,
            ScreenHeight = 3168,
            ScreenDpi = 510,
            GpuMode = "gfxstream",
            Description = "OnePlus 2024 flagship with Hasselblad camera emulation",
            SupportedAndroidVersions = new[] { "14" },
        },
        // Xiaomi
        new CuttlefishProfile
        {
            Id = "cf-xiaomi-14-ultra",
            Name = "Xiaomi 14 Ultra",
            Brand = "Xiaomi",
            Model = "24030PN60G",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 16384,
            StorageGb = 512,
            ScreenWidth = 1440,
            ScreenHeight = 3200,
            ScreenDpi = 522,
            GpuMode = "gfxstream",
            Description = "Xiaomi ultimate camera phone with Leica optics emulation",
            SupportedAndroidVersions = new[] { "14" },
        },
        // Generic / lightweight
        new CuttlefishProfile
        {
            Id = "cf-generic-phone",
            Name = "Generic Phone (Standard)",
            Brand = "Android",
            Model = "Cuttlefish Phone",
            Category = "midrange",
            CpuCores = 4,
            RamMb = 4096,
            StorageGb = 64,
            ScreenWidth = 1080,
            ScreenHeight = 2400,
            ScreenDpi = 420,
            GpuMode = "gfxstream",
            Description = "Standard Cuttlefish phone - lightweight, fast boot, all features",
            SupportedAndroidVersions = new[] { "15", "14", "13" },
        },
        // Samsung foldable
        new CuttlefishProfile
        {
            Id = "cf-samsung-zfold5",
            Name = "Samsung Galaxy Z Fold 5",
            Brand = "Samsung",
            Model = "SM-F946B",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 12288,
            StorageGb = 256,
            ScreenWidth = 1812,
            ScreenHeight = 2176,
            ScreenDpi = 374,
            GpuMode = "gfxstream",
            Description = "Samsung foldable flagship with dual-display emulation",
            SupportedAndroidVersions = new[] { "14", "13" },
        },
        // Tablets
        new CuttlefishProfile
        {
            Id = "cf-pixel-tablet",
            Name = "Google Pixel Tablet",
            Brand = "Google",
            Model = "Pixel Tablet",
            Category = "tablet",
            CpuCores = 8,
            RamMb = 8192,
            StorageGb = 128,
            ScreenWidth = 1600,
            ScreenHeight = 2560,
            ScreenDpi = 276,
            GpuMode = "gfxstream",
            Description = "Google tablet with hub mode and smart home integration",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
    };

    // ─── OS Images ────────────────────────────────────────────────────────
    private static readonly List<CuttlefishImage> _images = new()
    {
        new CuttlefishImage
        {
            Id = "cf-android-15",
            Name = "Android 15 (Vanilla Ice Cream)",
            AndroidVersion = "15",
            ApiLevel = 35,
            BuildId = "aosp-android15-release",
            Branch = "aosp-android15-release",
            Target = "aosp_cf_x86_64_phone-userdebug",
            HasGapps = false,
            IsDefault = true,
            Description = "Latest Android 15 with full Cuttlefish support - WebRTC, modem, sensors, GPS",
        },
        new CuttlefishImage
        {
            Id = "cf-android-15-gapps",
            Name = "Android 15 with Google Apps",
            AndroidVersion = "15",
            ApiLevel = 35,
            BuildId = "aosp-android15-release",
            Branch = "aosp-android15-release",
            Target = "aosp_cf_x86_64_phone-userdebug",
            HasGapps = true,
            IsDefault = false,
            Description = "Android 15 with Play Store, Gmail, Maps - flash GApps after boot",
        },
        new CuttlefishImage
        {
            Id = "cf-android-14",
            Name = "Android 14 (Upside Down Cake)",
            AndroidVersion = "14",
            ApiLevel = 34,
            BuildId = "aosp-android14-release",
            Branch = "aosp-android14-release",
            Target = "aosp_cf_x86_64_phone-userdebug",
            HasGapps = false,
            IsDefault = false,
            Description = "Android 14 - most stable, widest app compatibility",
        },
        new CuttlefishImage
        {
            Id = "cf-android-14-gapps",
            Name = "Android 14 with Google Apps",
            AndroidVersion = "14",
            ApiLevel = 34,
            BuildId = "aosp-android14-release",
            Branch = "aosp-android14-release",
            Target = "aosp_cf_x86_64_phone-userdebug",
            HasGapps = true,
            IsDefault = false,
            Description = "Android 14 with Play Store - recommended for most use cases",
        },
        new CuttlefishImage
        {
            Id = "cf-android-13",
            Name = "Android 13 (Tiramisu)",
            AndroidVersion = "13",
            ApiLevel = 33,
            BuildId = "aosp-android13-release",
            Branch = "aosp-android13-gsi",
            Target = "aosp_cf_x86_64_phone-userdebug",
            HasGapps = false,
            IsDefault = false,
            Description = "Android 13 - legacy support for older apps",
        },
    };

    public List<CuttlefishProfile> GetProfiles() => _profiles;
    public List<CuttlefishImage> GetImages() => _images;

    // ─── Status ───────────────────────────────────────────────────────────
    public async Task<CuttlefishStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = new CuttlefishStatus { IsConfigured = _configured };
        if (!_configured) return status;

        try
        {
            // Check host reachability
            var pingResult = await RunOnHostAsync("echo ok", ct);
            status.HostReachable = pingResult.Success;

            if (status.HostReachable)
            {
                // Check KVM
                var kvmResult = await RunOnHostAsync("ls /dev/kvm 2>/dev/null && echo 'kvm_ok'", ct);
                status.KvmAvailable = kvmResult.StandardOutput.Contains("kvm_ok");

                // Check Docker
                var dockerResult = await RunOnHostAsync("docker --version 2>/dev/null", ct);
                status.DockerAvailable = dockerResult.Success && dockerResult.StandardOutput.Contains("Docker");

                // Check Cuttlefish packages
                var cfResult = await RunOnHostAsync("which launch_cvd 2>/dev/null || dpkg -l | grep cuttlefish 2>/dev/null", ct);
                status.CuttlefishInstalled = cfResult.Success && !string.IsNullOrWhiteSpace(cfResult.StandardOutput);

                // CPU/Memory/Disk
                var cpuResult = await RunOnHostAsync("nproc", ct);
                status.HostCpuCores = cpuResult.Success ? cpuResult.StandardOutput.Trim() : "?";

                var memResult = await RunOnHostAsync("free -g | awk '/Mem:/ {print $2}'", ct);
                status.HostMemoryGb = memResult.Success ? memResult.StandardOutput.Trim() : "?";

                var diskResult = await RunOnHostAsync("df -BG / | awk 'NR==2 {print $4}'", ct);
                status.HostDiskGb = diskResult.Success ? diskResult.StandardOutput.Trim().TrimEnd('G') : "?";

                // GPU detection
                var gpuResult = await RunOnHostAsync("lspci 2>/dev/null | grep -i 'vga\\|3d\\|display' | head -1", ct);
                if (gpuResult.Success && !string.IsNullOrWhiteSpace(gpuResult.StandardOutput))
                {
                    status.GpuAvailable = true;
                    status.GpuModel = gpuResult.StandardOutput.Trim();
                }

                // Running devices
                var devices = await GetDevicesAsync(ct);
                status.TotalDevices = devices.Count;
                status.RunningDevices = devices.Count(d => d.State == "running");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Cuttlefish status");
        }

        return status;
    }

    // ─── Device Lifecycle ─────────────────────────────────────────────────
    public async Task<List<CuttlefishDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        if (!_configured) return new List<CuttlefishDevice>();

        var devices = new List<CuttlefishDevice>();
        try
        {
            // List Cuttlefish Docker containers
            var result = await RunOnHostAsync(
                "docker ps -a --filter 'label=mch.type=cuttlefish' --format '{{.Names}}|{{.Status}}|{{.Ports}}' 2>/dev/null", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                // Also check in-memory tracked devices
                lock (_lock)
                {
                    foreach (var kvp in _deviceStates)
                    {
                        devices.Add(BuildDeviceFromState(kvp.Key, kvp.Value, "stopped"));
                    }
                }
                return devices;
            }

            var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                var containerName = parts[0].Trim();
                var statusStr = parts[1].Trim();
                var deviceId = containerName.Replace("cvd-", "");

                var state = statusStr.StartsWith("Up") ? "running" : "stopped";

                CuttlefishDeviceState? devState;
                lock (_lock)
                {
                    _deviceStates.TryGetValue(deviceId, out devState);
                }

                if (devState != null)
                {
                    devices.Add(BuildDeviceFromState(deviceId, devState, state));
                }
                else
                {
                    devices.Add(new CuttlefishDevice
                    {
                        Id = deviceId,
                        Name = deviceId,
                        ContainerName = containerName,
                        State = state,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Cuttlefish devices");
        }

        return devices;
    }

    public async Task<CuttlefishDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        var devices = await GetDevicesAsync(ct);
        return devices.FirstOrDefault(d => d.Id == deviceId);
    }

    public async Task<CuttlefishDevice?> CreateDeviceAsync(CreateCuttlefishRequest request, CancellationToken ct = default)
    {
        if (!_configured) return null;

        var profile = _profiles.FirstOrDefault(p => p.Id == request.ProfileId);
        if (profile == null)
        {
            _logger.LogError("Cuttlefish profile not found: {ProfileId}", request.ProfileId);
            return null;
        }

        var image = _images.FirstOrDefault(i => i.Id == request.ImageId);
        if (image == null)
        {
            _logger.LogError("Cuttlefish image not found: {ImageId}", request.ImageId);
            return null;
        }

        try
        {
            // Determine device index and ports
            var existingDevices = await GetDevicesAsync(ct);
            var usedIndices = existingDevices.Select(d =>
            {
                var parts = d.Id.Split('-');
                return int.TryParse(parts.LastOrDefault(), out var idx) ? idx : 0;
            }).ToHashSet();

            var deviceIndex = 1;
            while (usedIndices.Contains(deviceIndex)) deviceIndex++;

            var deviceId = $"cf-{deviceIndex}";
            var containerName = $"cvd-{deviceId}";
            var deviceName = string.IsNullOrWhiteSpace(request.Name)
                ? $"{profile.Name} #{deviceIndex}"
                : request.Name;

            var adbPort = AdbPortBase + (deviceIndex - 1);
            var webRtcPort = WebRtcPortBase + (deviceIndex - 1) * 10;
            var controlPort = ControlPortBase + (deviceIndex - 1) * 10;

            // GPU mode
            var gpuMode = request.EnableGpu ? profile.GpuMode : "swiftshader_indirect";

            // Build Docker run command for Cuttlefish
            var dockerCmd = BuildDockerRunCommand(new CvdConfig
            {
                ContainerName = containerName,
                DeviceId = deviceId,
                Profile = profile,
                Image = image,
                AdbPort = adbPort,
                WebRtcPort = webRtcPort,
                ControlPort = controlPort,
                GpuMode = gpuMode,
            });

            _logger.LogInformation("Creating Cuttlefish VM {Name} ({Profile}): {Cmd}",
                deviceName, profile.Name, dockerCmd);

            var result = await RunOnHostAsync(dockerCmd, ct, timeoutMs: 120000);
            if (!result.Success)
            {
                _logger.LogError("Failed to create Cuttlefish VM: {Err}", result.StandardError);
                return null;
            }

            // Auto-provision Twilio number
            string? phoneNumber = null;
            if (request.AssignPhoneNumber && _twilioService.IsConfigured)
            {
                var number = await _twilioService.ProvisionNumberAsync(containerName, ct);
                phoneNumber = number?.PhoneNumber;
            }

            // Store device state
            var devState = new CuttlefishDeviceState
            {
                Name = deviceName,
                ContainerName = containerName,
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                ImageId = image.Id,
                AndroidVersion = image.AndroidVersion,
                Brand = profile.Brand,
                Model = profile.Model,
                CpuCores = profile.CpuCores,
                RamMb = profile.RamMb,
                StorageGb = profile.StorageGb,
                ScreenWidth = profile.ScreenWidth,
                ScreenHeight = profile.ScreenHeight,
                ScreenDpi = profile.ScreenDpi,
                AdbPort = adbPort,
                WebRtcPort = webRtcPort,
                ControlPort = controlPort,
                GpuMode = gpuMode,
                HasGapps = image.HasGapps,
                HasModem = profile.HasModem,
                HasGps = profile.HasGps,
                HasSensors = profile.HasSensors,
                HasCamera = profile.HasCamera,
                HasBiometrics = profile.HasBiometrics,
                PhoneNumber = phoneNumber,
                CreatedAt = DateTime.UtcNow,
            };

            lock (_lock)
            {
                _deviceStates[deviceId] = devState;
            }

            _logger.LogInformation("Created Cuttlefish VM {Name} on {Container} with WebRTC:{Port}",
                deviceName, containerName, webRtcPort);

            return BuildDeviceFromState(deviceId, devState, "creating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Cuttlefish VM");
            return null;
        }
    }

    public async Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";

            // Release Twilio number
            if (_twilioService.IsConfigured)
                await _twilioService.ReleaseNumberAsync(containerName, ct);

            // Stop and remove container
            await RunOnHostAsync($"docker stop {containerName} 2>/dev/null; docker rm -f {containerName} 2>/dev/null", ct);

            lock (_lock)
            {
                _deviceStates.Remove(deviceId);
            }

            _logger.LogInformation("Removed Cuttlefish VM {DeviceId}", deviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove Cuttlefish VM {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> RestartDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";
            var result = await RunOnHostAsync($"docker restart {containerName}", ct, timeoutMs: 60000);
            _logger.LogInformation("Restarted Cuttlefish VM {DeviceId}", deviceId);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart Cuttlefish VM {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<string?> GetStreamUrlAsync(string deviceId, CancellationToken ct = default)
    {
        var device = await GetDeviceAsync(deviceId, ct);
        if (device == null || device.State != "running") return null;
        return device.WebRtcUrl;
    }

    public async Task<string?> ExecShellAsync(string deviceId, string command, CancellationToken ct = default)
    {
        if (!_configured) return null;

        try
        {
            var containerName = $"cvd-{deviceId}";
            var result = await RunOnHostAsync(
                $"docker exec {containerName} adb -s 0.0.0.0:6520 shell {command}", ct);
            return result.Success ? result.StandardOutput : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exec shell on Cuttlefish {DeviceId}", deviceId);
            return null;
        }
    }

    // ─── Cuttlefish Environment Controls (Genymotion-like) ──────────────
    public async Task<bool> SetGpsLocationAsync(string deviceId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";
            // Use Cuttlefish env control for GPS
            var result = await RunOnHostAsync(
                $"docker exec {containerName} bash -c 'echo \"geo fix {longitude} {latitude}\" | nc -q 1 localhost 5554' 2>/dev/null || " +
                $"docker exec {containerName} adb -s 0.0.0.0:6520 emu geo fix {longitude} {latitude}",
                ct);

            if (result.Success)
            {
                lock (_lock)
                {
                    if (_deviceStates.TryGetValue(deviceId, out var state))
                        state.GpsLocation = $"{latitude},{longitude}";
                }
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set GPS on {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> SetBatteryAsync(string deviceId, int level, string status, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";
            var batteryCmd = status switch
            {
                "charging" => $"dumpsys battery set level {level} && dumpsys battery set status 2",
                "discharging" => $"dumpsys battery set level {level} && dumpsys battery set status 3",
                "full" => $"dumpsys battery set level 100 && dumpsys battery set status 5",
                "not_charging" => $"dumpsys battery set level {level} && dumpsys battery set status 4",
                _ => $"dumpsys battery set level {level}",
            };

            var result = await RunOnHostAsync(
                $"docker exec {containerName} adb -s 0.0.0.0:6520 shell '{batteryCmd}'", ct);

            if (result.Success)
            {
                lock (_lock)
                {
                    if (_deviceStates.TryGetValue(deviceId, out var state))
                    {
                        state.BatteryLevel = level;
                        state.BatteryStatus = status;
                    }
                }
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set battery on {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> SetNetworkAsync(string deviceId, string mode, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";
            var networkCmd = mode switch
            {
                "wifi" => "svc wifi enable && svc data disable",
                "cellular" => "svc wifi disable && svc data enable",
                "airplane" => "settings put global airplane_mode_on 1 && am broadcast -a android.intent.action.AIRPLANE_MODE --ez state true",
                "off" => "svc wifi disable && svc data disable",
                _ => "svc wifi enable",
            };

            var result = await RunOnHostAsync(
                $"docker exec {containerName} adb -s 0.0.0.0:6520 shell '{networkCmd}'", ct);

            if (result.Success)
            {
                lock (_lock)
                {
                    if (_deviceStates.TryGetValue(deviceId, out var state))
                        state.NetworkMode = mode;
                }
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set network on {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> RotateDisplayAsync(string deviceId, string orientation, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var containerName = $"cvd-{deviceId}";
            var rotValue = orientation switch
            {
                "portrait" => "0",
                "landscape" => "1",
                "reverse_portrait" => "2",
                "reverse_landscape" => "3",
                _ => "0",
            };

            var result = await RunOnHostAsync(
                $"docker exec {containerName} adb -s 0.0.0.0:6520 shell 'settings put system accelerometer_rotation 0 && settings put system user_rotation {rotValue}'", ct);

            if (result.Success)
            {
                lock (_lock)
                {
                    if (_deviceStates.TryGetValue(deviceId, out var state))
                        state.Orientation = orientation;
                }
            }

            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate display on {DeviceId}", deviceId);
            return false;
        }
    }

    // ─── Docker Command Builder ──────────────────────────────────────────
    private record CvdConfig
    {
        public string ContainerName { get; init; } = "";
        public string DeviceId { get; init; } = "";
        public CuttlefishProfile Profile { get; init; } = new();
        public CuttlefishImage Image { get; init; } = new();
        public int AdbPort { get; init; }
        public int WebRtcPort { get; init; }
        public int ControlPort { get; init; }
        public string GpuMode { get; init; } = "auto";
    }

    private string BuildDockerRunCommand(CvdConfig cfg)
    {
        var p = cfg.Profile;
        var webrtcPorts = $"-p {cfg.WebRtcPort}:8443";
        var adbPorts = $"-p {cfg.AdbPort}:6520";
        var controlPorts = $"-p {cfg.ControlPort}:1443";
        var udpPorts = $"-p {cfg.WebRtcPort + 1}-{cfg.WebRtcPort + 9}:15550-15558/udp -p {cfg.WebRtcPort + 1}-{cfg.WebRtcPort + 9}:15550-15558/tcp";

        return $"docker run -d " +
               $"--name {cfg.ContainerName} " +
               $"--privileged " +
               $"--label mch.type=cuttlefish " +
               $"--label mch.device-id={cfg.DeviceId} " +
               $"--label mch.profile={p.Id} " +
               $"--label mch.image={cfg.Image.Id} " +
               $"-v /dev/kvm:/dev/kvm " +
               $"{adbPorts} " +
               $"{webrtcPorts} " +
               $"{controlPorts} " +
               $"{udpPorts} " +
               $"-e CUTTLEFISH_INSTANCE=1 " +
               $"-e CF_CPUS={p.CpuCores} " +
               $"-e CF_MEMORY_MB={p.RamMb} " +
               $"-e CF_DISPLAY_WIDTH={p.ScreenWidth} " +
               $"-e CF_DISPLAY_HEIGHT={p.ScreenHeight} " +
               $"-e CF_DISPLAY_DPI={p.ScreenDpi} " +
               $"-e CF_GPU_MODE={cfg.GpuMode} " +
               $"-e CF_START_WEBRTC=true " +
               $"-e CF_ENABLE_MODEM={p.HasModem.ToString().ToLower()} " +
               $"-e CF_ENABLE_GPS={p.HasGps.ToString().ToLower()} " +
               $"us-docker.pkg.dev/android-cuttlefish-artifacts/cuttlefish-orchestration/cuttlefish-orchestrator:latest";
    }

    // ─── Helpers ──────────────────────────────────────────────────────────
    private CuttlefishDevice BuildDeviceFromState(string deviceId, CuttlefishDeviceState state, string runtimeState)
    {
        return new CuttlefishDevice
        {
            Id = deviceId,
            Name = state.Name,
            ContainerName = state.ContainerName,
            State = runtimeState,
            ProfileId = state.ProfileId,
            ProfileName = state.ProfileName,
            ImageId = state.ImageId,
            AndroidVersion = state.AndroidVersion,
            Brand = state.Brand,
            Model = state.Model,
            CpuCores = state.CpuCores,
            RamMb = state.RamMb,
            StorageGb = state.StorageGb,
            ScreenWidth = state.ScreenWidth,
            ScreenHeight = state.ScreenHeight,
            ScreenDpi = state.ScreenDpi,
            AdbPort = state.AdbPort,
            WebRtcPort = state.WebRtcPort,
            ControlPort = state.ControlPort,
            AdbAddress = $"{_hostAddress}:{state.AdbPort}",
            WebRtcUrl = $"https://{_hostAddress}:{state.WebRtcPort}",
            PhoneNumber = state.PhoneNumber,
            CreatedAt = state.CreatedAt,
            StartedAt = runtimeState == "running" ? state.CreatedAt : null,
            UptimeSeconds = runtimeState == "running" ? (long)(DateTime.UtcNow - state.CreatedAt).TotalSeconds : 0,
            GpuAccelerated = state.GpuMode != "swiftshader_indirect",
            HasGapps = state.HasGapps,
            HasModem = state.HasModem,
            HasGps = state.HasGps,
            HasSensors = state.HasSensors,
            HasCamera = state.HasCamera,
            HasBiometrics = state.HasBiometrics,
            GpsLocation = state.GpsLocation,
            BatteryLevel = state.BatteryLevel,
            BatteryStatus = state.BatteryStatus,
            NetworkMode = state.NetworkMode,
            Orientation = state.Orientation,
        };
    }

    private async Task<Domain.Models.CommandResult> RunOnHostAsync(string command, CancellationToken ct, int timeoutMs = 30000)
    {
        if (_hostAddress == "127.0.0.1" || _hostAddress == "localhost")
        {
            return await ProcessRunner.RunAsync("bash", $"-c \"{command.Replace("\"", "\\\"")}\"", timeoutMs, ct: ct);
        }
        else
        {
            // SSH to remote host
            var sshCmd = $"-o StrictHostKeyChecking=no -o ConnectTimeout=10 {_hostAddress} '{command.Replace("'", "'\\''")}'";
            return await ProcessRunner.RunAsync("ssh", sshCmd, timeoutMs, ct: ct);
        }
    }

    // Internal state tracking for device metadata
    private class CuttlefishDeviceState
    {
        public string Name { get; set; } = "";
        public string ContainerName { get; set; } = "";
        public string ProfileId { get; set; } = "";
        public string ProfileName { get; set; } = "";
        public string ImageId { get; set; } = "";
        public string AndroidVersion { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public int CpuCores { get; set; }
        public int RamMb { get; set; }
        public int StorageGb { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int ScreenDpi { get; set; }
        public int AdbPort { get; set; }
        public int WebRtcPort { get; set; }
        public int ControlPort { get; set; }
        public string GpuMode { get; set; } = "auto";
        public bool HasGapps { get; set; }
        public bool HasModem { get; set; }
        public bool HasGps { get; set; }
        public bool HasSensors { get; set; }
        public bool HasCamera { get; set; }
        public bool HasBiometrics { get; set; }
        public string? PhoneNumber { get; set; }
        public string? GpsLocation { get; set; }
        public int BatteryLevel { get; set; } = 100;
        public string BatteryStatus { get; set; } = "charging";
        public string NetworkMode { get; set; } = "wifi";
        public string Orientation { get; set; } = "portrait";
        public DateTime CreatedAt { get; set; }
    }
}
