using MobileControlHub.Domain.Models;

namespace MobileControlHub.Domain.Interfaces;

/// <summary>
/// Service for managing the RustDesk client integration.
/// Provides helpers to launch, configure, and check connectivity of the installed RustDesk client.
/// Does NOT rewrite RustDesk functionality -- wraps the installed client.
/// </summary>
public interface IRustDeskService
{
    /// <summary>Check if the RustDesk executable is available at the configured path.</summary>
    Task<bool> IsInstalledAsync(CancellationToken ct = default);

    /// <summary>Get the RustDesk version if available.</summary>
    Task<string?> GetVersionAsync(CancellationToken ct = default);

    /// <summary>Check if the RustDesk process is currently running.</summary>
    bool IsRunning();

    /// <summary>Launch the RustDesk client.</summary>
    Task<bool> LaunchAsync(CancellationToken ct = default);

    /// <summary>Test connectivity to the configured relay server.</summary>
    Task<bool> TestRelayConnectivityAsync(string host, int port, CancellationToken ct = default);

    /// <summary>Test connectivity to the configured ID/rendezvous server.</summary>
    Task<bool> TestIdServerConnectivityAsync(string host, int port, CancellationToken ct = default);

    /// <summary>Test connectivity to the VPS host.</summary>
    Task<bool> TestVpsConnectivityAsync(string host, int port, CancellationToken ct = default);

    /// <summary>Get the overall RustDesk status string.</summary>
    string GetStatusDescription();
}
