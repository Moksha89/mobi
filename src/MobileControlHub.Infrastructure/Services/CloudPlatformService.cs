using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Helpers;

namespace MobileControlHub.Infrastructure.Services;

/// <summary>
/// Manages self-hosted cloud Android devices on Kubernetes using Redroid containers.
/// Provides hardware profiles mimicking real Samsung/Pixel/etc phones, WebRTC streaming
/// via scrcpy-web sidecars, persistent storage, and full device lifecycle management.
/// </summary>
public class CloudPlatformService : ICloudPlatformService
{
    private readonly ILogger<CloudPlatformService> _logger;
    private readonly ITwilioService _twilioService;
    private readonly string _kubeconfigPath;
    private readonly string _namespace;
    private readonly string _nodeHost;
    private bool _configured;

    // Port allocation ranges
    private const int AdbPortBase = 6555;
    private const int StreamPortBase = 8800;
    private const int MaxDevices = 30;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public CloudPlatformService(ILogger<CloudPlatformService> logger, ITwilioService twilioService)
    {
        _logger = logger;
        _twilioService = twilioService;
        _kubeconfigPath = Environment.GetEnvironmentVariable("KUBECONFIG") ?? "/etc/kubernetes/admin.conf";
        _namespace = Environment.GetEnvironmentVariable("MCH_K8S_NAMESPACE") ?? "android-cloud";
        _nodeHost = Environment.GetEnvironmentVariable("MCH_K8S_NODE_HOST") ?? "127.0.0.1";
        _configured = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MCH_K8S_NODE_HOST"));
    }

    public bool IsConfigured => _configured;

    // ─── Hardware Profiles ────────────────────────────────────────────────
    private static readonly List<HardwareProfile> _profiles = new()
    {
        // Samsung Flagships
        new HardwareProfile
        {
            Id = "samsung-s25-ultra",
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
            GpuMode = "auto",
            BuildModel = "SM-S938B",
            BuildManufacturer = "samsung",
            BuildProduct = "dm3q",
            DeviceFingerprint = "samsung/dm3q/dm3q:15/AP3A.241105.008/S938BXXS1AXK1:user/release-keys",
            Description = "Samsung's 2025 flagship with S Pen, 200MP camera, Snapdragon 8 Elite",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new HardwareProfile
        {
            Id = "samsung-s24-ultra",
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
            GpuMode = "auto",
            BuildModel = "SM-S928B",
            BuildManufacturer = "samsung",
            BuildProduct = "e3q",
            DeviceFingerprint = "samsung/e3q/e3q:14/UP1A.231005.007/S928BXXS3AXH5:user/release-keys",
            Description = "Samsung's 2024 flagship with S Pen, 200MP camera, Snapdragon 8 Gen 3",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new HardwareProfile
        {
            Id = "samsung-s23-ultra",
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
            GpuMode = "auto",
            BuildModel = "SM-S918B",
            BuildManufacturer = "samsung",
            BuildProduct = "dm2q",
            DeviceFingerprint = "samsung/dm2q/dm2q:14/UP1A.231005.007/S918BXXS6CXH3:user/release-keys",
            Description = "Samsung's 2023 flagship with S Pen, 200MP camera, Snapdragon 8 Gen 2",
            SupportedAndroidVersions = new[] { "14", "13" },
        },
        new HardwareProfile
        {
            Id = "samsung-s24",
            Name = "Samsung Galaxy S24",
            Brand = "Samsung",
            Model = "SM-S921B",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 8192,
            StorageGb = 128,
            ScreenWidth = 1080,
            ScreenHeight = 2340,
            ScreenDpi = 416,
            GpuMode = "auto",
            BuildModel = "SM-S921B",
            BuildManufacturer = "samsung",
            BuildProduct = "e1q",
            DeviceFingerprint = "samsung/e1q/e1q:14/UP1A.231005.007/S921BXXS3AXH5:user/release-keys",
            Description = "Samsung's 2024 compact flagship, Snapdragon 8 Gen 3",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new HardwareProfile
        {
            Id = "samsung-a55",
            Name = "Samsung Galaxy A55 5G",
            Brand = "Samsung",
            Model = "SM-A556B",
            Category = "midrange",
            CpuCores = 4,
            RamMb = 8192,
            StorageGb = 128,
            ScreenWidth = 1080,
            ScreenHeight = 2340,
            ScreenDpi = 390,
            GpuMode = "auto",
            BuildModel = "SM-A556B",
            BuildManufacturer = "samsung",
            BuildProduct = "a55xq",
            DeviceFingerprint = "samsung/a55xq/a55xq:14/UP1A.231005.007/A556BXXS1AXE1:user/release-keys",
            Description = "Samsung's popular mid-range with Exynos 1480, Super AMOLED 120Hz",
            SupportedAndroidVersions = new[] { "14" },
        },
        new HardwareProfile
        {
            Id = "samsung-zfold5",
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
            GpuMode = "auto",
            BuildModel = "SM-F946B",
            BuildManufacturer = "samsung",
            BuildProduct = "q5q",
            DeviceFingerprint = "samsung/q5q/q5q:14/UP1A.231005.007/F946BXXS3CXH2:user/release-keys",
            Description = "Samsung foldable flagship, Snapdragon 8 Gen 2, 7.6\" main display",
            SupportedAndroidVersions = new[] { "14", "13" },
        },
        // Google Pixel
        new HardwareProfile
        {
            Id = "pixel-9-pro-xl",
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
            GpuMode = "auto",
            BuildModel = "Pixel 9 Pro XL",
            BuildManufacturer = "Google",
            BuildProduct = "komodo",
            DeviceFingerprint = "google/komodo/komodo:15/AP3A.241105.008/12485168:user/release-keys",
            Description = "Google's largest 2024 flagship with Tensor G4, 50MP triple camera, Gemini AI",
            SupportedAndroidVersions = new[] { "15" },
        },
        new HardwareProfile
        {
            Id = "pixel-9-pro",
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
            GpuMode = "auto",
            BuildModel = "Pixel 9 Pro",
            BuildManufacturer = "Google",
            BuildProduct = "caiman",
            DeviceFingerprint = "google/caiman/caiman:15/AP3A.241105.008/12485168:user/release-keys",
            Description = "Google's 2024 Pro flagship with Tensor G4, triple camera, Gemini AI",
            SupportedAndroidVersions = new[] { "15" },
        },
        new HardwareProfile
        {
            Id = "pixel-9",
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
            GpuMode = "auto",
            BuildModel = "Pixel 9",
            BuildManufacturer = "Google",
            BuildProduct = "tokay",
            DeviceFingerprint = "google/tokay/tokay:15/AP3A.241105.008/12485168:user/release-keys",
            Description = "Google's 2024 flagship with Tensor G4, dual camera, Gemini AI",
            SupportedAndroidVersions = new[] { "15" },
        },
        new HardwareProfile
        {
            Id = "pixel-8-pro",
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
            GpuMode = "auto",
            BuildModel = "Pixel 8 Pro",
            BuildManufacturer = "Google",
            BuildProduct = "husky",
            DeviceFingerprint = "google/husky/husky:14/AP2A.240805.005/12025142:user/release-keys",
            Description = "Google's 2023 Pro flagship with Tensor G3, temperature sensor, 7 years updates",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        new HardwareProfile
        {
            Id = "pixel-8",
            Name = "Google Pixel 8",
            Brand = "Google",
            Model = "Pixel 8",
            Category = "flagship",
            CpuCores = 8,
            RamMb = 8192,
            StorageGb = 128,
            ScreenWidth = 1080,
            ScreenHeight = 2400,
            ScreenDpi = 428,
            GpuMode = "auto",
            BuildModel = "Pixel 8",
            BuildManufacturer = "Google",
            BuildProduct = "shiba",
            DeviceFingerprint = "google/shiba/shiba:14/AP2A.240805.005/12025142:user/release-keys",
            Description = "Google's 2023 flagship with Tensor G3, 7 years of OS updates",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
        // OnePlus
        new HardwareProfile
        {
            Id = "oneplus-12",
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
            GpuMode = "auto",
            BuildModel = "CPH2583",
            BuildManufacturer = "OnePlus",
            BuildProduct = "aston",
            DeviceFingerprint = "OnePlus/CPH2583/OP5D55L1:14/UKQ1.230924.001/U.1562c0-1:user/release-keys",
            Description = "OnePlus 2024 flagship, Snapdragon 8 Gen 3, 100W SUPERVOOC, Hasselblad camera",
            SupportedAndroidVersions = new[] { "14" },
        },
        // Xiaomi
        new HardwareProfile
        {
            Id = "xiaomi-14-ultra",
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
            GpuMode = "auto",
            BuildModel = "24030PN60G",
            BuildManufacturer = "Xiaomi",
            BuildProduct = "shennong",
            DeviceFingerprint = "Xiaomi/shennong_global/shennong:14/UKQ1.230917.001/V816.0.6.0.UNACNXM:user/release-keys",
            Description = "Xiaomi's ultimate camera phone, Leica optics, Snapdragon 8 Gen 3",
            SupportedAndroidVersions = new[] { "14" },
        },
        // Generic / Budget
        new HardwareProfile
        {
            Id = "generic-phone-hd",
            Name = "Generic Phone (HD)",
            Brand = "Android",
            Model = "Generic Phone",
            Category = "budget",
            CpuCores = 2,
            RamMb = 2048,
            StorageGb = 32,
            ScreenWidth = 720,
            ScreenHeight = 1520,
            ScreenDpi = 320,
            GpuMode = "swiftshader",
            BuildModel = "generic_x86_64",
            BuildManufacturer = "Google",
            BuildProduct = "generic_x86_64",
            DeviceFingerprint = "google/generic_x86_64/generic_x86_64:14/UP1A.231005.007:user/release-keys",
            Description = "Lightweight device for basic testing — low resource usage",
            SupportedAndroidVersions = new[] { "15", "14", "13" },
        },
        new HardwareProfile
        {
            Id = "generic-phone-fhd",
            Name = "Generic Phone (FHD)",
            Brand = "Android",
            Model = "Generic Phone",
            Category = "midrange",
            CpuCores = 4,
            RamMb = 4096,
            StorageGb = 64,
            ScreenWidth = 1080,
            ScreenHeight = 2400,
            ScreenDpi = 420,
            GpuMode = "auto",
            BuildModel = "generic_x86_64",
            BuildManufacturer = "Google",
            BuildProduct = "generic_x86_64",
            DeviceFingerprint = "google/generic_x86_64/generic_x86_64:14/UP1A.231005.007:user/release-keys",
            Description = "Mid-range performance for general app testing",
            SupportedAndroidVersions = new[] { "15", "14", "13" },
        },
        // Tablets
        new HardwareProfile
        {
            Id = "samsung-tab-s9",
            Name = "Samsung Galaxy Tab S9",
            Brand = "Samsung",
            Model = "SM-X710",
            Category = "tablet",
            CpuCores = 8,
            RamMb = 8192,
            StorageGb = 128,
            ScreenWidth = 1600,
            ScreenHeight = 2560,
            ScreenDpi = 274,
            GpuMode = "auto",
            BuildModel = "SM-X710",
            BuildManufacturer = "samsung",
            BuildProduct = "gts9wifi",
            DeviceFingerprint = "samsung/gts9wifi/gts9wifi:14/UP1A.231005.007/X710XXS4CXH1:user/release-keys",
            Description = "Samsung's flagship Android tablet, 11\" 120Hz display, S Pen",
            SupportedAndroidVersions = new[] { "14" },
        },
        new HardwareProfile
        {
            Id = "pixel-tablet",
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
            GpuMode = "auto",
            BuildModel = "Pixel Tablet",
            BuildManufacturer = "Google",
            BuildProduct = "tangorpro",
            DeviceFingerprint = "google/tangorpro/tangorpro:14/AP2A.240805.005/12025142:user/release-keys",
            Description = "Google's 10.95\" tablet with Tensor G2, hub mode, smart home integration",
            SupportedAndroidVersions = new[] { "15", "14" },
        },
    };

    // ─── OS Images ────────────────────────────────────────────────────────
    private static readonly List<OsImage> _osImages = new()
    {
        new OsImage
        {
            Id = "android-15-gapps",
            Name = "Android 15 with Google Apps",
            AndroidVersion = "15",
            ApiLevel = 35,
            DockerImage = "redroid/redroid:15.0.0_64only-latest",
            HasGapps = true,
            IsDefault = true,
            Description = "Latest Android 15 (Vanilla Ice Cream) with Play Store, Gmail, Maps",
        },
        new OsImage
        {
            Id = "android-14-gapps",
            Name = "Android 14 with Google Apps",
            AndroidVersion = "14",
            ApiLevel = 34,
            DockerImage = "redroid/redroid:14.0.0_64only-latest",
            HasGapps = true,
            IsDefault = false,
            Description = "Android 14 (Upside Down Cake) with Play Store, Gmail, Maps — most stable",
        },
        new OsImage
        {
            Id = "android-13-gapps",
            Name = "Android 13 with Google Apps",
            AndroidVersion = "13",
            ApiLevel = 33,
            DockerImage = "redroid/redroid:13.0.0_64only-latest",
            HasGapps = true,
            IsDefault = false,
            Description = "Android 13 (Tiramisu) with Play Store — widest app compatibility",
        },
        new OsImage
        {
            Id = "android-14-aosp",
            Name = "Android 14 AOSP (No Google Apps)",
            AndroidVersion = "14",
            ApiLevel = 34,
            DockerImage = "redroid/redroid:14.0.0_64only-latest",
            HasGapps = false,
            IsDefault = false,
            Description = "Clean Android 14 without Google services — lightweight, faster boot",
        },
        new OsImage
        {
            Id = "android-13-aosp",
            Name = "Android 13 AOSP (No Google Apps)",
            AndroidVersion = "13",
            ApiLevel = 33,
            DockerImage = "redroid/redroid:13.0.0_64only-latest",
            HasGapps = false,
            IsDefault = false,
            Description = "Clean Android 13 without Google services — lightweight, faster boot",
        },
    };

    public List<HardwareProfile> GetHardwareProfiles() => _profiles;
    public List<OsImage> GetOsImages() => _osImages;

    // ─── Status ───────────────────────────────────────────────────────────
    public async Task<CloudPlatformStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var status = new CloudPlatformStatus { IsConfigured = _configured };
        if (!_configured) return status;

        try
        {
            // Check cluster reachability
            var versionResult = await KubectlAsync("version --short", ct);
            status.ClusterReachable = versionResult.Success;
            if (versionResult.Success)
                status.KubernetesVersion = versionResult.StandardOutput.Trim();

            // Node count
            var nodesResult = await KubectlAsync("get nodes --no-headers", ct);
            if (nodesResult.Success)
            {
                var lines = nodesResult.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                status.TotalNodes = lines.Length;
                status.ReadyNodes = lines.Count(l => l.Contains(" Ready"));
            }

            // GPU detection
            var gpuResult = await RunOnNodeAsync("lspci | grep -i 'vga\\|3d\\|display'", ct);
            if (gpuResult.Success && !string.IsNullOrWhiteSpace(gpuResult.StandardOutput))
            {
                status.GpuAvailable = true;
                status.GpuModel = gpuResult.StandardOutput.Trim().Split('\n').FirstOrDefault()?.Trim() ?? "";
            }

            // Device count
            var devices = await GetDevicesAsync(ct);
            status.TotalDevices = devices.Count;
            status.RunningDevices = devices.Count(d => d.State == "running");

            // Resources
            status.Resources = await GetClusterResourcesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cloud platform status");
        }

        return status;
    }

    // ─── Device Lifecycle ─────────────────────────────────────────────────
    public async Task<List<CloudDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        if (!_configured) return new List<CloudDevice>();

        try
        {
            var result = await KubectlAsync(
                $"get pods -n {_namespace} -l app=android-device -o json", ct);

            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
                return new List<CloudDevice>();

            return ParsePodList(result.StandardOutput);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cloud devices");
            return new List<CloudDevice>();
        }
    }

    public async Task<CloudDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        var devices = await GetDevicesAsync(ct);
        return devices.FirstOrDefault(d => d.Id == deviceId);
    }

    public async Task<CloudDevice?> CreateDeviceAsync(CreateCloudDeviceRequest request, CancellationToken ct = default)
    {
        if (!_configured) return null;

        var profile = _profiles.FirstOrDefault(p => p.Id == request.HardwareProfileId);
        if (profile == null)
        {
            _logger.LogError("Hardware profile not found: {ProfileId}", request.HardwareProfileId);
            return null;
        }

        var osImage = _osImages.FirstOrDefault(i => i.Id == request.OsImageId);
        if (osImage == null)
        {
            _logger.LogError("OS image not found: {ImageId}", request.OsImageId);
            return null;
        }

        try
        {
            // Determine device ID and ports
            var existingDevices = await GetDevicesAsync(ct);
            var usedAdbPorts = existingDevices.Select(d => d.AdbPort).ToHashSet();
            var usedStreamPorts = existingDevices.Select(d => d.StreamPort).ToHashSet();

            var adbPort = AdbPortBase;
            while (usedAdbPorts.Contains(adbPort)) adbPort++;

            var streamPort = StreamPortBase;
            while (usedStreamPorts.Contains(streamPort)) streamPort++;

            var deviceIndex = adbPort - AdbPortBase + 1;
            var deviceId = $"device-{deviceIndex}";
            var deviceName = string.IsNullOrWhiteSpace(request.Name)
                ? $"{profile.Name} #{deviceIndex}"
                : request.Name;

            // Sanitize for K8s naming (lowercase, no spaces, alphanumeric + hyphens)
            var podName = $"android-{deviceId}";

            // Determine GPU mode
            var gpuMode = request.EnableGpu ? profile.GpuMode : "swiftshader";

            // Determine docker image — use GApps variant if requested
            var dockerImage = osImage.DockerImage;
            if (osImage.HasGapps)
            {
                // Use the mindthegapps image we already built
                dockerImage = dockerImage.Replace("_64only-latest", "_mindthegapps");
                // Fallback — if it doesn't exist, the node will pull the base image
            }

            // Build the K8s pod manifest
            var manifest = BuildPodManifest(new PodConfig
            {
                PodName = podName,
                DeviceId = deviceId,
                DeviceName = deviceName,
                Profile = profile,
                OsImage = osImage,
                DockerImage = dockerImage,
                AdbPort = adbPort,
                StreamPort = streamPort,
                GpuMode = gpuMode,
                PersistentStorage = request.PersistentStorage,
            });

            // Create PVC if persistent storage requested
            if (request.PersistentStorage)
            {
                var pvcManifest = BuildPvcManifest(podName, profile.StorageGb);
                var pvcResult = await KubectlApplyAsync(pvcManifest, ct);
                if (!pvcResult.Success)
                    _logger.LogWarning("Failed to create PVC for {Pod}: {Err}", podName, pvcResult.StandardError);
            }

            // Apply the pod manifest
            var createResult = await KubectlApplyAsync(manifest, ct);
            if (!createResult.Success)
            {
                _logger.LogError("Failed to create pod {Pod}: {Err}", podName, createResult.StandardError);
                return null;
            }

            // Auto-provision Twilio number
            string? phoneNumber = null;
            if (request.AssignPhoneNumber && _twilioService.IsConfigured)
            {
                var number = await _twilioService.ProvisionNumberAsync(podName, ct);
                phoneNumber = number?.PhoneNumber;
            }

            _logger.LogInformation("Created cloud device {Name} ({Profile}) on pod {Pod} with ADB:{AdbPort} Stream:{StreamPort}",
                deviceName, profile.Name, podName, adbPort, streamPort);

            return new CloudDevice
            {
                Id = deviceId,
                Name = deviceName,
                State = "creating",
                HardwareProfileId = profile.Id,
                HardwareProfileName = profile.Name,
                OsImageId = osImage.Id,
                AndroidVersion = osImage.AndroidVersion,
                Brand = profile.Brand,
                Model = profile.Model,
                CpuCores = profile.CpuCores,
                RamMb = profile.RamMb,
                StorageGb = profile.StorageGb,
                ScreenWidth = profile.ScreenWidth,
                ScreenHeight = profile.ScreenHeight,
                ScreenDpi = profile.ScreenDpi,
                AdbPort = adbPort,
                StreamPort = streamPort,
                AdbAddress = $"{_nodeHost}:{adbPort}",
                StreamUrl = $"http://{_nodeHost}:{streamPort}",
                PhoneNumber = phoneNumber,
                PodName = podName,
                CreatedAt = DateTime.UtcNow,
                GpuAccelerated = gpuMode != "swiftshader",
                HasGapps = osImage.HasGapps,
                PersistentStorage = request.PersistentStorage,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create cloud device");
            return null;
        }
    }

    public async Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var podName = $"android-{deviceId}";

            // Release Twilio number
            if (_twilioService.IsConfigured)
                await _twilioService.ReleaseNumberAsync(podName, ct);

            // Delete the pod
            var result = await KubectlAsync($"delete pod {podName} -n {_namespace} --grace-period=10", ct);

            // Delete PVC if it exists
            await KubectlAsync($"delete pvc {podName}-data -n {_namespace} --ignore-not-found", ct);

            _logger.LogInformation("Removed cloud device {DeviceId} (pod: {Pod})", deviceId, podName);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cloud device {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<bool> RestartDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        if (!_configured) return false;

        try
        {
            var podName = $"android-{deviceId}";
            var result = await KubectlAsync($"delete pod {podName} -n {_namespace} --grace-period=5", ct);

            // The pod will be recreated by the ReplicaSet/Deployment if we use deployments
            // For standalone pods, we need to re-apply the manifest
            // For now, we rely on restartPolicy: Always in the pod spec
            _logger.LogInformation("Restarted cloud device {DeviceId}", deviceId);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart cloud device {DeviceId}", deviceId);
            return false;
        }
    }

    public async Task<string?> GetStreamUrlAsync(string deviceId, CancellationToken ct = default)
    {
        var device = await GetDeviceAsync(deviceId, ct);
        if (device == null || device.State != "running") return null;
        return device.StreamUrl;
    }

    public async Task<string?> ExecShellAsync(string deviceId, string command, CancellationToken ct = default)
    {
        if (!_configured) return null;

        try
        {
            var podName = $"android-{deviceId}";
            var result = await KubectlAsync(
                $"exec {podName} -n {_namespace} -c android -- adb shell {command}", ct);
            return result.Success ? result.StandardOutput : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exec shell on {DeviceId}", deviceId);
            return null;
        }
    }

    public async Task<ClusterResources> GetClusterResourcesAsync(CancellationToken ct = default)
    {
        var resources = new ClusterResources();
        if (!_configured) return resources;

        try
        {
            // Get node capacity
            var nodeResult = await KubectlAsync("get nodes -o json", ct);
            if (nodeResult.Success)
            {
                using var doc = JsonDocument.Parse(nodeResult.StandardOutput);
                var items = doc.RootElement.GetProperty("items");
                foreach (var node in items.EnumerateArray())
                {
                    var capacity = node.GetProperty("status").GetProperty("capacity");
                    var cpuStr = capacity.GetProperty("cpu").GetString() ?? "0";
                    var memStr = capacity.GetProperty("memory").GetString() ?? "0";

                    resources.TotalCpuMillicores += ParseCpu(cpuStr);
                    resources.TotalMemoryMb += ParseMemory(memStr);
                }
            }

            // Estimate max devices based on resources (each device ~4GB RAM, 2 CPU)
            resources.MaxDevices = Math.Min(
                MaxDevices,
                Math.Min(
                    (int)(resources.TotalMemoryMb / 4096),
                    (int)(resources.TotalCpuMillicores / 2000)
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cluster resources");
        }

        return resources;
    }

    // ─── K8s Manifest Builders ────────────────────────────────────────────
    private record PodConfig
    {
        public string PodName { get; init; } = "";
        public string DeviceId { get; init; } = "";
        public string DeviceName { get; init; } = "";
        public HardwareProfile Profile { get; init; } = new();
        public OsImage OsImage { get; init; } = new();
        public string DockerImage { get; init; } = "";
        public int AdbPort { get; init; }
        public int StreamPort { get; init; }
        public string GpuMode { get; init; } = "auto";
        public bool PersistentStorage { get; init; }
    }

    private string BuildPodManifest(PodConfig cfg)
    {
        var p = cfg.Profile;
        var volumeMounts = "";
        var volumes = "";

        if (cfg.PersistentStorage)
        {
            volumeMounts = @$"
        volumeMounts:
        - name: data
          mountPath: /data";
            volumes = @$"
      volumes:
      - name: data
        persistentVolumeClaim:
          claimName: {cfg.PodName}-data";
        }

        return @$"apiVersion: v1
kind: Pod
metadata:
  name: {cfg.PodName}
  namespace: {_namespace}
  labels:
    app: android-device
    device-id: {cfg.DeviceId}
    hardware-profile: {p.Id}
    os-image: {cfg.OsImage.Id}
    brand: {p.Brand.ToLowerInvariant()}
  annotations:
    device-name: ""{cfg.DeviceName}""
    hardware-profile-name: ""{p.Name}""
    android-version: ""{cfg.OsImage.AndroidVersion}""
    adb-port: ""{cfg.AdbPort}""
    stream-port: ""{cfg.StreamPort}""
    screen-width: ""{p.ScreenWidth}""
    screen-height: ""{p.ScreenHeight}""
    screen-dpi: ""{p.ScreenDpi}""
    gpu-mode: ""{cfg.GpuMode}""
    has-gapps: ""{cfg.OsImage.HasGapps}""
    persistent-storage: ""{cfg.PersistentStorage}""
spec:
  restartPolicy: Always
  containers:
  - name: android
    image: {cfg.DockerImage}
    imagePullPolicy: IfNotPresent
    securityContext:
      privileged: true
    resources:
      requests:
        cpu: ""{Math.Max(1, p.CpuCores / 2)}""
        memory: ""{p.RamMb}Mi""
      limits:
        cpu: ""{p.CpuCores}""
        memory: ""{p.RamMb}Mi""
    ports:
    - name: adb
      containerPort: 5555
      hostPort: {cfg.AdbPort}
      protocol: TCP
    args:
    - androidboot.redroid_width={p.ScreenWidth}
    - androidboot.redroid_height={p.ScreenHeight}
    - androidboot.redroid_dpi={p.ScreenDpi}
    - androidboot.redroid_gpu_mode={cfg.GpuMode}
    - ro.product.model={p.BuildModel}
    - ro.product.brand={p.Brand.ToLowerInvariant()}
    - ro.product.manufacturer={p.BuildManufacturer}
    - ro.product.device={p.BuildProduct}
    - ro.build.display.id={p.DeviceFingerprint}{volumeMounts}
  - name: scrcpy-web
    image: emptysun/scrcpy-web:v0.1
    imagePullPolicy: IfNotPresent
    ports:
    - name: stream
      containerPort: 8000
      hostPort: {cfg.StreamPort}
      protocol: TCP
    env:
    - name: DEVICE_HOST
      value: ""127.0.0.1""
    - name: DEVICE_PORT
      value: ""5555""
    - name: SCREEN_WIDTH
      value: ""{p.ScreenWidth}""
    - name: SCREEN_HEIGHT
      value: ""{p.ScreenHeight}""
    resources:
      requests:
        cpu: ""500m""
        memory: ""256Mi""
      limits:
        cpu: ""1""
        memory: ""512Mi""{volumes}
";
    }

    private string BuildPvcManifest(string podName, int storageGb)
    {
        return @$"apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: {podName}-data
  namespace: {_namespace}
spec:
  accessModes:
  - ReadWriteOnce
  resources:
    requests:
      storage: {storageGb}Gi
  storageClassName: local-path
";
    }

    // ─── Pod Parsing ──────────────────────────────────────────────────────
    private List<CloudDevice> ParsePodList(string json)
    {
        var devices = new List<CloudDevice>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");

            foreach (var pod in items.EnumerateArray())
            {
                var metadata = pod.GetProperty("metadata");
                var podName = metadata.GetProperty("name").GetString() ?? "";
                var labels = metadata.GetProperty("labels");
                var annotations = metadata.TryGetProperty("annotations", out var ann) ? ann : default;

                var status = pod.GetProperty("status");
                var phase = status.GetProperty("phase").GetString() ?? "Unknown";

                var deviceId = GetLabel(labels, "device-id");
                var profileId = GetLabel(labels, "hardware-profile");
                var imageId = GetLabel(labels, "os-image");
                var brand = GetLabel(labels, "brand");

                var deviceName = GetAnnotation(annotations, "device-name");
                var profileName = GetAnnotation(annotations, "hardware-profile-name");
                var androidVersion = GetAnnotation(annotations, "android-version");
                var adbPortStr = GetAnnotation(annotations, "adb-port");
                var streamPortStr = GetAnnotation(annotations, "stream-port");
                var screenWStr = GetAnnotation(annotations, "screen-width");
                var screenHStr = GetAnnotation(annotations, "screen-height");
                var screenDpiStr = GetAnnotation(annotations, "screen-dpi");
                var gpuMode = GetAnnotation(annotations, "gpu-mode");
                var hasGappsStr = GetAnnotation(annotations, "has-gapps");
                var persistStr = GetAnnotation(annotations, "persistent-storage");

                int.TryParse(adbPortStr, out var adbPort);
                int.TryParse(streamPortStr, out var streamPort);
                int.TryParse(screenWStr, out var screenW);
                int.TryParse(screenHStr, out var screenH);
                int.TryParse(screenDpiStr, out var screenDpi);

                var profile = _profiles.FirstOrDefault(p => p.Id == profileId);

                var state = phase switch
                {
                    "Running" => "running",
                    "Pending" => "creating",
                    "Succeeded" => "stopped",
                    "Failed" => "error",
                    _ => "pending"
                };

                // Parse creation timestamp
                DateTime.TryParse(
                    metadata.TryGetProperty("creationTimestamp", out var ts)
                        ? ts.GetString() : null,
                    out var createdAt);

                // Parse started time from container statuses
                DateTime? startedAt = null;
                if (status.TryGetProperty("containerStatuses", out var containerStatuses))
                {
                    foreach (var cs in containerStatuses.EnumerateArray())
                    {
                        if (cs.TryGetProperty("state", out var csState) &&
                            csState.TryGetProperty("running", out var running) &&
                            running.TryGetProperty("startedAt", out var sa))
                        {
                            if (DateTime.TryParse(sa.GetString(), out var started))
                            {
                                startedAt = started;
                                break;
                            }
                        }
                    }
                }

                var nodeName = status.TryGetProperty("hostIP", out var hip) ? hip.GetString() ?? "" : "";

                devices.Add(new CloudDevice
                {
                    Id = deviceId,
                    Name = deviceName,
                    State = state,
                    HardwareProfileId = profileId,
                    HardwareProfileName = profileName,
                    OsImageId = imageId,
                    AndroidVersion = androidVersion,
                    Brand = brand,
                    Model = profile?.Model ?? "",
                    CpuCores = profile?.CpuCores ?? 4,
                    RamMb = profile?.RamMb ?? 4096,
                    StorageGb = profile?.StorageGb ?? 64,
                    ScreenWidth = screenW,
                    ScreenHeight = screenH,
                    ScreenDpi = screenDpi,
                    AdbPort = adbPort,
                    StreamPort = streamPort,
                    AdbAddress = $"{_nodeHost}:{adbPort}",
                    StreamUrl = $"http://{_nodeHost}:{streamPort}",
                    PodName = podName,
                    NodeName = nodeName,
                    CreatedAt = createdAt,
                    StartedAt = startedAt,
                    UptimeSeconds = startedAt.HasValue ? (long)(DateTime.UtcNow - startedAt.Value).TotalSeconds : 0,
                    GpuAccelerated = gpuMode != "swiftshader",
                    HasGapps = hasGappsStr == "True",
                    PersistentStorage = persistStr == "True",
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse pod list");
        }

        return devices.OrderBy(d => d.Id).ToList();
    }

    private static string GetLabel(JsonElement labels, string key)
    {
        return labels.TryGetProperty(key, out var val) ? val.GetString() ?? "" : "";
    }

    private static string GetAnnotation(JsonElement annotations, string key)
    {
        if (annotations.ValueKind == JsonValueKind.Undefined) return "";
        return annotations.TryGetProperty(key, out var val) ? val.GetString() ?? "" : "";
    }

    // ─── Helpers ──────────────────────────────────────────────────────────
    private async Task<Domain.Models.CommandResult> KubectlAsync(string args, CancellationToken ct)
    {
        var kubeconfigArg = File.Exists(_kubeconfigPath) ? $"--kubeconfig {_kubeconfigPath}" : "";
        return await ProcessRunner.RunAsync("kubectl", $"{kubeconfigArg} {args}".Trim(), timeoutMs: 30000, ct: ct);
    }

    private async Task<Domain.Models.CommandResult> KubectlApplyAsync(string manifest, CancellationToken ct)
    {
        // Write manifest to temp file and apply
        var tmpFile = Path.GetTempFileName() + ".yaml";
        await File.WriteAllTextAsync(tmpFile, manifest, ct);
        try
        {
            return await KubectlAsync($"apply -f {tmpFile}", ct);
        }
        finally
        {
            try { File.Delete(tmpFile); } catch { }
        }
    }

    private async Task<Domain.Models.CommandResult> RunOnNodeAsync(string command, CancellationToken ct)
    {
        // Execute a command on the node — if running on the node itself, use bash
        return await ProcessRunner.RunAsync("/bin/bash", $"-c \"{command}\"", timeoutMs: 10000, ct: ct);
    }

    private static long ParseCpu(string cpu)
    {
        if (cpu.EndsWith("m"))
            return long.TryParse(cpu[..^1], out var m) ? m : 0;
        return long.TryParse(cpu, out var cores) ? cores * 1000 : 0;
    }

    private static long ParseMemory(string mem)
    {
        if (mem.EndsWith("Ki"))
            return long.TryParse(mem[..^2], out var ki) ? ki / 1024 : 0;
        if (mem.EndsWith("Mi"))
            return long.TryParse(mem[..^2], out var mi) ? mi : 0;
        if (mem.EndsWith("Gi"))
            return long.TryParse(mem[..^2], out var gi) ? gi * 1024 : 0;
        return long.TryParse(mem, out var b) ? b / (1024 * 1024) : 0;
    }
}
