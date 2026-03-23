namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing Cuttlefish-based cloud Android VMs.
/// Provides Genymotion-like features: full QEMU/KVM Android VMs with WebRTC streaming,
/// virtual modem, GPS, sensors, camera, battery, and biometrics simulation.
/// </summary>
public interface ICuttlefishService
{
    /// <summary>Whether the Cuttlefish host is configured and reachable.</summary>
    bool IsConfigured { get; }

    /// <summary>Get platform status and host health.</summary>
    Task<CuttlefishStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Get all available device profiles.</summary>
    List<CuttlefishProfile> GetProfiles();

    /// <summary>Get all available Android images.</summary>
    List<CuttlefishImage> GetImages();

    /// <summary>List all running Cuttlefish VMs.</summary>
    Task<List<CuttlefishDevice>> GetDevicesAsync(CancellationToken ct = default);

    /// <summary>Get a single device by ID.</summary>
    Task<CuttlefishDevice?> GetDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Create a new Cuttlefish Android VM.</summary>
    Task<CuttlefishDevice?> CreateDeviceAsync(CreateCuttlefishRequest request, CancellationToken ct = default);

    /// <summary>Stop and remove a Cuttlefish VM.</summary>
    Task<bool> RemoveDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Restart a Cuttlefish VM.</summary>
    Task<bool> RestartDeviceAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Get the WebRTC streaming URL for a device.</summary>
    Task<string?> GetStreamUrlAsync(string deviceId, CancellationToken ct = default);

    /// <summary>Execute an ADB shell command on a device.</summary>
    Task<string?> ExecShellAsync(string deviceId, string command, CancellationToken ct = default);

    /// <summary>Set GPS location on a device.</summary>
    Task<bool> SetGpsLocationAsync(string deviceId, double latitude, double longitude, CancellationToken ct = default);

    /// <summary>Set battery level/state on a device.</summary>
    Task<bool> SetBatteryAsync(string deviceId, int level, string status, CancellationToken ct = default);

    /// <summary>Set network mode on a device.</summary>
    Task<bool> SetNetworkAsync(string deviceId, string mode, CancellationToken ct = default);

    /// <summary>Rotate the device display.</summary>
    Task<bool> RotateDisplayAsync(string deviceId, string orientation, CancellationToken ct = default);
}

/// <summary>Cuttlefish host status.</summary>
public class CuttlefishStatus
{
    public bool IsConfigured { get; set; }
    public bool IsAvailable { get; set; }
    public bool HostReachable { get; set; }
    public string HostAddress { get; set; } = string.Empty;
    public bool KvmAvailable { get; set; }
    public bool DockerAvailable { get; set; }
    public string DockerVersion { get; set; } = string.Empty;
    public bool CuttlefishInstalled { get; set; }
    public int TotalDevices { get; set; }
    public int RunningDevices { get; set; }
    public string HostCpuCores { get; set; } = string.Empty;
    public string HostMemoryGb { get; set; } = string.Empty;
    public string HostDiskGb { get; set; } = string.Empty;
    public bool GpuAvailable { get; set; }
    public string GpuModel { get; set; } = string.Empty;
}

/// <summary>
/// A Cuttlefish device profile - determines VM resources and device identity.
/// These map to real device specs (Samsung, Pixel, etc.) similar to Genymotion recipes.
/// </summary>
public class CuttlefishProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Category { get; set; } = "flagship";
    public int CpuCores { get; set; } = 4;
    public int RamMb { get; set; } = 4096;
    public int StorageGb { get; set; } = 64;
    public int ScreenWidth { get; set; } = 1080;
    public int ScreenHeight { get; set; } = 2400;
    public int ScreenDpi { get; set; } = 420;
    public string GpuMode { get; set; } = "auto";
    public string Description { get; set; } = string.Empty;
    public string[] SupportedAndroidVersions { get; set; } = Array.Empty<string>();
    // Cuttlefish-specific features
    public bool HasModem { get; set; } = true;
    public bool HasGps { get; set; } = true;
    public bool HasSensors { get; set; } = true;
    public bool HasCamera { get; set; } = true;
    public bool HasBiometrics { get; set; } = true;
}

/// <summary>An Android image for Cuttlefish VMs.</summary>
public class CuttlefishImage
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AndroidVersion { get; set; } = string.Empty;
    public int ApiLevel { get; set; }
    public string BuildId { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public bool HasGapps { get; set; }
    public bool IsDefault { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>A running Cuttlefish Android VM instance.</summary>
public class CuttlefishDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string State { get; set; } = "pending";
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public string ImageId { get; set; } = string.Empty;
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
    public int WebRtcPort { get; set; }
    public int ControlPort { get; set; }
    public string AdbAddress { get; set; } = string.Empty;
    public string WebRtcUrl { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public long UptimeSeconds { get; set; }
    public bool GpuAccelerated { get; set; }
    public bool HasGapps { get; set; }
    // Cuttlefish-specific capabilities
    public bool HasModem { get; set; }
    public bool HasGps { get; set; }
    public bool HasSensors { get; set; }
    public bool HasCamera { get; set; }
    public bool HasBiometrics { get; set; }
    public string? GpsLocation { get; set; }
    public int BatteryLevel { get; set; } = 100;
    public string BatteryStatus { get; set; } = "charging";
    public string NetworkMode { get; set; } = "wifi";
    public string Orientation { get; set; } = "portrait";
}

/// <summary>Request to create a new Cuttlefish VM.</summary>
public class CreateCuttlefishRequest
{
    public string Name { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ImageId { get; set; } = string.Empty;
    public bool AssignPhoneNumber { get; set; } = true;
    public bool EnableGpu { get; set; } = true;
}
