namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing self-hosted cloud Android devices on Kubernetes.
/// Supports hardware profiles (Samsung, Pixel, etc.), OS images (Android 13-15),
/// WebRTC streaming via scrcpy-web, persistent storage, and device lifecycle.
/// </summary>
public interface ICloudPlatformService
{
    /// <summary>Whether the K8s cloud platform is configured and reachable.</summary>
    bool IsConfigured { get; }

    /// <summary>Get platform configuration and cluster status.</summary>
    Task<CloudPlatformStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Get all available hardware profiles.</summary>
    List<HardwareProfile> GetHardwareProfiles();

    /// <summary>Get all available OS images.</summary>
    List<OsImage> GetOsImages();

    /// <summary>List all running cloud devices.</summary>
    Task<List<CloudDevice>> GetDevicesAsync(CancellationToken ct = default);

    /// <summary>Get a single device by ID.</summary>
    Task<CloudDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Create a new cloud Android device.</summary>
    Task<CloudDevice?> CreateDeviceAsync(CreateCloudDeviceRequest request, CancellationToken ct = default);

    /// <summary>Stop and remove a cloud device.</summary>
    Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Restart a cloud device.</summary>
    Task<bool> RestartDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Get the WebRTC streaming URL for a device.</summary>
    Task<string?> GetStreamUrlAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Execute an ADB shell command on a device.</summary>
    Task<string?> ExecShellAsync(string deviceId, string command, CancellationToken ct = default);

    /// <summary>Get cluster resource usage (CPU, RAM, storage).</summary>
    Task<ClusterResources> GetClusterResourcesAsync(CancellationToken ct = default);
}

/// <summary>Cloud platform configuration and status.</summary>
public class CloudPlatformStatus
{
    public bool IsConfigured { get; set; }
    public bool ClusterReachable { get; set; }
    public string KubernetesVersion { get; set; } = string.Empty;
    public int TotalNodes { get; set; }
    public int ReadyNodes { get; set; }
    public int TotalDevices { get; set; }
    public int RunningDevices { get; set; }
    public bool GpuAvailable { get; set; }
    public string GpuModel { get; set; } = string.Empty;
    public ClusterResources Resources { get; set; } = new();
}

/// <summary>Cluster resource usage.</summary>
public class ClusterResources
{
    public long TotalCpuMillicores { get; set; }
    public long UsedCpuMillicores { get; set; }
    public long TotalMemoryMb { get; set; }
    public long UsedMemoryMb { get; set; }
    public long TotalStorageGb { get; set; }
    public long UsedStorageGb { get; set; }
    public int MaxDevices { get; set; }
}

/// <summary>
/// A hardware profile simulating a real phone's specifications.
/// Determines CPU, RAM, screen resolution, DPI, GPU mode, and device identity.
/// </summary>
public class HardwareProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Category { get; set; } = "flagship"; // flagship, midrange, budget, tablet
    public int CpuCores { get; set; } = 4;
    public int RamMb { get; set; } = 4096;
    public int StorageGb { get; set; } = 64;
    public int ScreenWidth { get; set; } = 1080;
    public int ScreenHeight { get; set; } = 2400;
    public int ScreenDpi { get; set; } = 420;
    public string GpuMode { get; set; } = "auto"; // auto, host, guest, swiftshader
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string BuildModel { get; set; } = string.Empty;
    public string BuildManufacturer { get; set; } = string.Empty;
    public string BuildProduct { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string[] SupportedAndroidVersions { get; set; } = Array.Empty<string>();
}

/// <summary>An Android OS image available for cloud devices.</summary>
public class OsImage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public int ApiLevel { get; set; }
    public string DockerImage { get; set; } = string.Empty;
    public bool HasGapps { get; set; }
    public bool IsDefault { get; set; }
    public string Architecture { get; set; } = "x86_64";
    public string Description { get; set; } = string.Empty;
}

/// <summary>A running cloud Android device instance.</summary>
public class CloudDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "pending"; // pending, creating, running, error, stopping, stopped
    public string HardwareProfileId { get; set; } = string.Empty;
    public string HardwareProfileName { get; set; } = string.Empty;
    public string OsImageId { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public int RamMb { get; set; }
    public int StorageGb { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int ScreenDpi { get; set; }
    public int AdbPort { get; set; }
    public int StreamPort { get; set; }
    public string AdbAddress { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PodName { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public long UptimeSeconds { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMb { get; set; }
    public bool GpuAccelerated { get; set; }
    public bool HasGapps { get; set; }
    public bool PersistentStorage { get; set; }
}

/// <summary>Request to create a new cloud device.</summary>
public class CreateCloudDeviceRequest
{
    public string Name { get; set; } = string.Empty;
    public string HardwareProfileId { get; set; } = string.Empty;
    public string OsImageId { get; set; } = string.Empty;
    public bool AssignPhoneNumber { get; set; } = true;
    public bool PersistentStorage { get; set; } = true;
    public bool EnableGpu { get; set; } = true;
}
